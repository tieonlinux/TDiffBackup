using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading;
using DiffBackup.Backup;
using DiffBackup.Logger;
using Moq;
using Xunit;

namespace DiffBackupTests
{
    public class BackupServiceTests : IDisposable
    {
        public BackupServiceTests()
        {
            _temporaryPath = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temporaryPath);
            _mockedLog = new Mock<ITlog>(MockBehavior.Loose);
            _worldFile = DownloadWorldFile();
            _worldFileHash = new Lazy<string>(() =>
            {
                using var s = File.OpenRead(_worldFile);
                return BackupUtils.HashFile(s);
            });
        }

        public void Dispose()
        {
            if (Directory.Exists(_temporaryPath))
            {
                Directory.Delete(_temporaryPath, true);
            }
        }

        private readonly string _temporaryPath;
        private readonly string _worldFile;
        private readonly Lazy<string> _worldFileHash;
        private readonly Mock<ITlog> _mockedLog;
        private readonly Random _random = new Random();

        private string DownloadWorldFile(
            string url =
                @"https://github.com/TEdit/Terraria-Map-Editor/raw/5c4afae20b/tests/World%20Files%201.4.0.3/SM_Classic.wld")
        {
            using var client = new WebClient();
            var dest = Path.Join(_temporaryPath, $"world{Guid.NewGuid():N}.wld");
            client.DownloadFile(url, dest);
            return dest;
        }


        private string CreateEmptyArchive()
        {
            var res = Path.Join(_temporaryPath, $"{Guid.NewGuid():N}.zip");
            using var _ = ZipFile.Open(res, ZipArchiveMode.Create);
            return res;
        }

        private string CreateArchiveWithOldEntry(DateTime dateTime, string fileName)
        {
            var res = Path.Join(_temporaryPath, $"{Guid.NewGuid():N}.zip");
            using var archive = ZipFile.Open(res, ZipArchiveMode.Create);
            var entry = archive.CreateEntry(fileName);
            entry.LastWriteTime = dateTime;
            return res;
        }

        [Fact]
        public void TestCreateDiff2xSameFile()
        {
            var originalHash = _worldFileHash.Value;
            using var cancellationTokenSource = new CancellationTokenSource();
            using var service = new BackupService(_mockedLog.Object);
            var date = DateTime.Now;
            service.StartBackup(_worldFile, date, cancellationTokenSource.Token).Wait(cancellationTokenSource.Token);
            Assert.True(Directory.Exists(BackupUtils.GetRepoFilePath(_worldFile)));
            var repo = new BackupRepository(BackupUtils.GetRepoFilePath(_worldFile));
            Assert.Single(repo.Entries);
            var reference = BackupUtils.HashFile(repo.Entries.First());
            var backupName = BackupUtils.FormatWorldFileName(date, reference);
            Assert.True(repo.Entries.First().Name == backupName);

            Assert.True(service.ShouldThrottle);

            date = DateTime.Now;
            service.ResetThrottle();
            Assert.False(service.ShouldThrottle);
            service.StartBackup(_worldFile, date, cancellationTokenSource.Token).Wait(cancellationTokenSource.Token);
            repo = new BackupRepository(BackupUtils.GetRepoFilePath(_worldFile));
            Assert.Equal(2, repo.Entries.Count);
            var times = service.ListBackup(_worldFile, cancellationTokenSource.Token);
            Assert.Equal(2, times.Count);

            var restored = service.Restore(_worldFile, date, cancellationTokenSource.Token);
            Assert.True(restored);
            {
                using var s = File.OpenRead(_worldFile);
                Assert.Equal(originalHash, BackupUtils.HashFile(s));
            }
        }

        [Fact]
        public void TestCreateDiffOnFreshNewRepo()
        {
            var originalHash = _worldFileHash.Value;
            using var service = new BackupService(_mockedLog.Object);
            var date = DateTime.Now;
            service.StartBackup(_worldFile, date, CancellationToken.None).Wait();
            Assert.True(Directory.Exists(BackupUtils.GetRepoFilePath(_worldFile)));
            var repo = new BackupRepository(BackupUtils.GetRepoFilePath(_worldFile));
            Assert.Single(repo.Entries);
            var reference = BackupUtils.HashFile(repo.Entries.First());
            var backupName = BackupUtils.FormatWorldFileName(date, reference);
            Assert.True(repo.Entries.First().Name == backupName);

            Assert.True(service.ShouldThrottle);

            var restored = service.Restore(_worldFile, date, CancellationToken.None);
            Assert.True(restored);
            {
                using var s = File.OpenRead(_worldFile);
                Assert.Equal(originalHash, BackupUtils.HashFile(s));
            }
        }


        [Fact]
        public void TestRestoreUsingDirectCopy()
        {
            var originalHash = _worldFileHash.Value;
            using var cancellationTokenSource = new CancellationTokenSource();
            using var service = new BackupService(_mockedLog.Object);
            var date = DateTime.Now;
            var originalDate = date;
            service.StartBackup(_worldFile, date, cancellationTokenSource.Token).Wait(cancellationTokenSource.Token);
            var repo = new BackupRepository(BackupUtils.GetRepoFilePath(_worldFile));
            Assert.Single(repo.Entries);
            var reference = BackupUtils.HashFile(repo.Entries.First());
            Assert.Equal(originalHash, reference);
            var backupName = BackupUtils.FormatWorldFileName(date, reference);
            Assert.True(repo.Entries.First().Name == backupName);

            Assert.True(service.ShouldThrottle);

            // randomly edit the world file
            var content = File.ReadAllBytes(_worldFile);
            const int blockSize = 512;
            Assert.True(content.Length > blockSize);

            for (var i = 0; i < content.Length / blockSize; ++i)
            {
                if ((i + 1) * blockSize < content.Length && _random.NextDouble() < 0.1)
                {
                    _random.NextBytes(new Span<byte>(content).Slice(i * blockSize, blockSize));
                }
            }

            _random.NextBytes(new Span<byte>(content).Slice(content.Length - blockSize, blockSize));

            File.WriteAllBytes(_worldFile, content);

            {
                using var s = File.OpenRead(_worldFile);
                var alteredHash = BackupUtils.HashFile(s);
                Assert.NotEqual(originalHash, alteredHash);
            }


            date += TimeSpan.FromSeconds(1);
            service.ResetThrottle();
            Assert.False(service.ShouldThrottle);
            service.StartBackup(_worldFile, date, cancellationTokenSource.Token).Wait(cancellationTokenSource.Token);
            repo = new BackupRepository(BackupUtils.GetRepoFilePath(_worldFile));
            Assert.Equal(2, repo.Entries.Count);
            var times = service.ListBackup(_worldFile, cancellationTokenSource.Token);
            Assert.Equal(2, times.Count);

            var restored = service.Restore(_worldFile, originalDate, cancellationTokenSource.Token);
            Assert.True(restored);
            {
                using var s = File.OpenRead(_worldFile);
                Assert.Equal(originalHash, BackupUtils.HashFile(s));
            }
        }

        [Fact]
        public void TestRestoreUsingTheDiffFile()
        {
            var originalHash = _worldFileHash.Value;
            using var cancellationTokenSource = new CancellationTokenSource();
            using var service = new BackupService(_mockedLog.Object);
            var date = DateTime.Now;
            service.StartBackup(_worldFile, date, cancellationTokenSource.Token).Wait(cancellationTokenSource.Token);
            var repo = new BackupRepository(BackupUtils.GetRepoFilePath(_worldFile));
            Assert.Single(repo.Entries);
            var reference = BackupUtils.HashFile(repo.Entries.First());
            Assert.Equal(originalHash, reference);
            var backupName = BackupUtils.FormatWorldFileName(date, reference);
            Assert.True(repo.Entries.First().Name == backupName);

            Assert.True(service.ShouldThrottle);

            // randomly edit the world file
            var content = File.ReadAllBytes(_worldFile);
            const int blockSize = 512;
            Assert.True(content.Length > blockSize);

            for (var i = 0; i < content.Length / blockSize; ++i)
            {
                if ((i + 1) * blockSize < content.Length && _random.NextDouble() < 0.1)
                {
                    _random.NextBytes(new Span<byte>(content).Slice(i * blockSize, blockSize));
                }
            }

            _random.NextBytes(new Span<byte>(content).Slice(content.Length - blockSize, blockSize));

            File.WriteAllBytes(_worldFile, content);
            string alteredHash;

            {
                using var s = File.OpenRead(_worldFile);
                alteredHash = BackupUtils.HashFile(s);
                Assert.NotEqual(originalHash, alteredHash);
            }


            date += TimeSpan.FromSeconds(1);
            service.ResetThrottle();
            Assert.False(service.ShouldThrottle);
            service.StartBackup(_worldFile, date, cancellationTokenSource.Token).Wait(cancellationTokenSource.Token);
            repo = new BackupRepository(BackupUtils.GetRepoFilePath(_worldFile));
            Assert.Equal(2, repo.Entries.Count);
            var times = service.ListBackup(_worldFile, cancellationTokenSource.Token);
            Assert.Equal(2, times.Count);

            var restored = service.Restore(_worldFile, date, cancellationTokenSource.Token);
            Assert.True(restored);
            {
                using var s = File.OpenRead(_worldFile);
                Assert.Equal(alteredHash, BackupUtils.HashFile(s));
            }
        }
    }
}