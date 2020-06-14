using System;
using System.IO;
using System.IO.Compression;
using DiffBackup;
using DiffBackup.Backup;
using Xunit;

namespace DiffBackupTests
{
    public class DefaultBackupStrategyTests : IDisposable
    {
        private readonly string _temporaryPath; 
        public DefaultBackupStrategyTests()
        {
            _temporaryPath = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temporaryPath);
        }


        BackupRepository CreateEmptyRepo()
        {
            var res = Path.Join(_temporaryPath, $"{Guid.NewGuid():N}");
            return new BackupRepository(res, allowCreation: true);
        }
        
        BackupRepository CreateRepoWithOldEntry(DateTime dateTime, string fileName)
        {
            var repo = CreateEmptyRepo();
            var entry = repo.CreateEntry(fileName);
            entry.LastWriteTime = dateTime;
            return repo;
        }
        
        [Fact]
        public void TestGetReferenceOnEmptyRepo()
        {
            var strategy = new DefaultBackupStrategy();
            var repo = CreateEmptyRepo();
            Assert.Null(strategy.GetReference(repo, DateTime.Now));
        }
        
        [Fact]
        public void TestGetReferenceOnOldEntry()
        {
            var fname = $"{Guid.NewGuid():N}.test";
            var strategy = new DefaultBackupStrategy();
            var repo = CreateRepoWithOldEntry(DateTime.Now - strategy.ForceFullBackupTimeSpan, fname);
            Assert.Null(strategy.GetReference(repo, DateTime.Now));
        }
        
        [Fact]
        public void TestGetReferenceOnRecentEntry()
        {
            var dateTime = DateTime.Now;
            var fname = BackupUtils.FormatWorldFileName(dateTime, Guid.NewGuid().ToString("N"));
            var strategy = new DefaultBackupStrategy();
            var repo = CreateRepoWithOldEntry(dateTime, fname);
            var res = strategy.GetReference(repo, DateTime.Now);
            Assert.NotNull(res);
            Assert.True((res.LastWriteTime - dateTime).TotalSeconds < 0.1);
            Assert.Equal(fname, res.Name);
        }

        public void Dispose()
        {
            if (Directory.Exists(_temporaryPath))
            {
                Directory.Delete(_temporaryPath, true);
            }
        }
    }
}