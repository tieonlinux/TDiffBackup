using System;
using System.IO;
using System.Linq;
using DiffBackup.Backup;
using DiffBackup.Backup.Config;
using DiffBackup.Backup.Config.Json;
using DiffBackup.Logger;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace DiffBackupTests.Backup
{
    public class DefaultBackupStrategyTests : IDisposable
    {
        public DefaultBackupStrategyTests(ITestOutputHelper testOutputHelper)
        {
            JsonConverters.Install();
            _testOutputHelper = testOutputHelper;
            _temporaryPath = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            _mockedLog = new Mock<ITlog>(MockBehavior.Loose);
            _mockedConfig = new Mock<StrategyConfig>(MockBehavior.Loose){ CallBase = true };
            Directory.CreateDirectory(_temporaryPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(_temporaryPath))
            {
                Directory.Delete(_temporaryPath, true);
            }
        }

        private readonly ITestOutputHelper _testOutputHelper;
        private readonly string _temporaryPath;
        private readonly Mock<ITlog> _mockedLog;
        private Mock<StrategyConfig> _mockedConfig;


        private BackupRepository CreateEmptyRepo()
        {
            var res = Path.Join(_temporaryPath, $"{Guid.NewGuid():N}");
            return new BackupRepository(res, true);
        }

        private BackupRepository CreateRepoWithOldEntry(DateTime dateTime, string fileName)
        {
            var repo = CreateEmptyRepo();
            var entry = repo.CreateEntry(fileName);
            entry.LastWriteTime = dateTime;
            return repo;
        }

        [Fact]
        public void TestGetReferenceOnEmptyRepo()
        {
            var strategy = new DefaultBackupStrategy(_mockedLog.Object, new StrategyConfig());
            var repo = CreateEmptyRepo();
            Assert.Null(strategy.GetReference(repo, DateTime.Now));
        }

        [Fact]
        public void TestGetReferenceOnOldEntry()
        {
            var fname = $"{Guid.NewGuid():N}.test";
            var strategy = new DefaultBackupStrategy(_mockedLog.Object, new StrategyConfig());
            var dateTime = DateTime.Now;
            var repo = CreateRepoWithOldEntry(dateTime - strategy.ForceFullBackupTimeSpan, fname);
            Assert.Null(strategy.GetReference(repo, dateTime));
        }

        [Fact]
        public void TestGetReferenceOnRecentEntry()
        {
            var dateTime = DateTime.Now;
            var fname = BackupUtils.FormatWorldFileName(dateTime, Guid.NewGuid().ToString("N"));
            var strategy = new DefaultBackupStrategy(_mockedLog.Object, new StrategyConfig());
            var repo = CreateRepoWithOldEntry(dateTime, fname);
            var res = strategy.GetReference(repo, dateTime);
            Assert.NotNull(res);
            Assert.True((res!.LastWriteTime - dateTime).TotalSeconds < 0.1);
            Assert.Equal(fname, res.Name);
        }


        [Fact]
        public void TestListExpiredEntriesOnFakeRepo()
        {
            var dateTime = new DateTime(2020, 6, 29, 16, 0, 0);
            var repo = CreateEmptyRepo();
            var c = 0;
            var fhash = Guid.NewGuid().ToString("N");
            for (var ts = TimeSpan.Zero; ts < TimeSpan.FromDays(600); ts += TimeSpan.FromDays(1))
            {
                var effectiveDt = dateTime - ts;

                var wldName = BackupUtils.FormatWorldFileName(effectiveDt, fhash);
                var dir = BackupUtils.GetRepoSubFolderNameForDate(effectiveDt);
                wldName = Path.Join(dir, wldName);
                var entry = repo.CreateEntry(wldName);
                entry.LastWriteTime = effectiveDt;
                if (c++ % 7 == 0)
                {
                    for (var tsdiff = TimeSpan.Zero; ts < TimeSpan.FromDays(1); ts += TimeSpan.FromHours(3))
                    {
                        effectiveDt = dateTime - ts + tsdiff;
                        dir = BackupUtils.GetRepoSubFolderNameForDate(effectiveDt);
                        var diffName = BackupUtils.FormatDiffFileName(effectiveDt, fhash);
                        diffName = Path.Join(dir, diffName);
                        var diffEntry = repo.CreateEntry(diffName);
                        diffEntry.LastWriteTime = effectiveDt;
                    }

                    fhash = Guid.NewGuid().ToString("N");
                }
            }

            var strategy = new DefaultBackupStrategy(_mockedLog.Object, new StrategyConfig());

            var res = strategy.ListExpiredEntries(repo, dateTime);
            _testOutputHelper.WriteLine(res.ToString());
            Assert.Equal(175, res.Count);
            Assert.NotEqual(repo.Entries.Count, res.Count);
            Assert.DoesNotContain(repo.Entries.First(), res);
            Assert.Empty(res.Where(entry => entry.LastWriteTime > dateTime - TimeSpan.FromDays(7)));
        }
    }
}