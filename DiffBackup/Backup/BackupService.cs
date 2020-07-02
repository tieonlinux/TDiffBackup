using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using deltaq_tie_tdiff.BsDiff;
using DiffBackup.Backup.Config;
using DiffBackup.Logger;
using Terraria;
using TShockAPI.Extensions;

#nullable enable

namespace DiffBackup.Backup
{
    public class BackupService : IDisposable, IBackupService
    {
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly SemaphoreSlim _saveDiffSemaphore = new SemaphoreSlim(1, 1);
        public readonly AllConfig Config;
        public readonly ITlog Log;
        private Stopwatch _saveDiffStopWatch = new Stopwatch();
        private readonly Stopwatch _scheduledCleanupStopWatch = Stopwatch.StartNew();

        public BackupService(ITlog log, AllConfig config, IBackupStrategy? strategy = null,
            BackupIOWorker? worker = null)
        {
            Log = log;
            Config = config.Clone<AllConfig>();
            Strategy = strategy ?? new DefaultBackupStrategy(Log, config.Strategy);
            Worker = worker ?? new BackupIOWorker(log);
            var env = Environment.GetEnvironmentVariables();

            // ReSharper disable once InconsistentNaming
            const string TdiffOverwriteThrottleTimeSpan = nameof(TdiffOverwriteThrottleTimeSpan);

            if (env.Contains(TdiffOverwriteThrottleTimeSpan))
            {
                ThrottleTimeSpan =
                    TimeSpan.FromSeconds(
                        double.Parse(Environment.GetEnvironmentVariable(TdiffOverwriteThrottleTimeSpan)));
            }

            Task.Factory.StartNew(CleanupScheduleLoop, _cancellationTokenSource.Token,
                TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
        }

        public TimeSpan ThrottleTimeSpan
        {
            get => Config.Internal.ThrottleTimeSpan;
            set => Config.Internal.ThrottleTimeSpan = value;
        }

        public bool ShouldThrottle =>
            ThrottleTimeSpan > TimeSpan.Zero && _saveDiffStopWatch.IsRunning &&
            _saveDiffStopWatch.Elapsed < ThrottleTimeSpan;

        public BackupIOWorker Worker { get; }


        public IBackupStrategy Strategy { get; }

        public async Task StartBackup(string path, DateTime? dateTime = null,
            CancellationToken cancellationToken = default)
        {
            await BackupSaveAsync(path, dateTime, cancellationToken);
        }

        public async Task<IList<BackupRepositoryEntry>> StartCleanup(string worldPath,
            CancellationToken cancellationToken = default)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,
                _cancellationTokenSource.Token);
            var repo = new BackupRepository(BackupUtils.GetRepoFilePath(Path.GetFullPath(worldPath)));
            return await Worker.IoScheduleFunction(() => Cleanup(repo), cts.Token);
        }


        public List<DateTime> ListBackup(string path, CancellationToken cancellationToken)
        {
            async Task<List<DateTime>> ListDate()
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,
                    _cancellationTokenSource.Token);
                var repo = new BackupRepository(BackupUtils.GetRepoFilePath(path));
                cancellationToken.ThrowIfCancellationRequested();
                var entries =
                    await Worker.IoScheduleFunction(() => BackupUtils.ListBackup(repo.Entries).ToList(), cts.Token);
                return entries.Select(entry => entry.DateTime).ToList();
            }

            try
            {
                var task = Task.Run(ListDate, cancellationToken);
                return task.Result;
            }
            catch (FileNotFoundException)
            {
                return new List<DateTime>();
            }
        }

        public bool Restore(string path, DateTime date, CancellationToken cancellationToken)
        {
            var res = Task.Run(
                async () => await Worker.IoScheduleFunction(() => DoRestore(path, date, cancellationToken),
                    cancellationToken), cancellationToken);
            return res.Result;
        }

        public void Dispose()
        {
            Log.LogDebug($"{nameof(BackupService)} Dispose");
            Worker.Dispose();
            _saveDiffSemaphore.Dispose();
            Strategy.Dispose();
            _cancellationTokenSource.Cancel(false);
        }

        private async Task CleanupScheduleLoop()
        {
            var cancellationToken = _cancellationTokenSource.Token;
            if (Config.Strategy.Cleanup.ScheduleTimeSpan <= TimeSpan.Zero)
            {
                Log.LogDebug("CleanupScheduleLoop no need to run");
                return;
            }

            var random = new Random();
            await Task.Delay(TimeSpan.FromSeconds(random.NextDouble() * 60), cancellationToken);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (_scheduledCleanupStopWatch.Elapsed >= Config.Strategy.Cleanup.ScheduleTimeSpan)
                    {
                        await StartCleanup(Main.WorldPath, cancellationToken);
                        _scheduledCleanupStopWatch.Restart();
                    }

                    await Task.Delay(
                        Config.Strategy.Cleanup.ScheduleTimeSpan + TimeSpan.FromSeconds(random.NextDouble() * 60),
                        cancellationToken);
                }
            }
            catch (OperationCanceledException e)
            {
                Log.LogDebug("Loop cancelled");
                Log.LogDebug(e.BuildExceptionString(), TraceLevel.Error);
            }
        }

        private bool DoRestore(string path, DateTime date, CancellationToken cancellationToken)
        {
            var repo = new BackupRepository(BackupUtils.GetRepoFilePath(path));
            cancellationToken.ThrowIfCancellationRequested();
            var entry = BackupUtils.ListBackup(repo).First(e => Math.Abs((e.DateTime - date).TotalSeconds) <= 1);
            if (!entry.IsDiff)
            {
                // just extract and we're done
                using var zs = entry.Entry.Open();
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None);
                zs.CopyTo(fs);
                return true;
            }
            else
            {
                byte[] diffBuff;
                {
                    using var zs = entry.Entry.Open();
                    using var diff = new MemoryStream();
                    zs.CopyTo(diff);
                    diffBuff = diff.ToArray();
                }

                byte[] srcBuff;
                {
                    using var src = new MemoryStream();
                    Debug.Assert(entry.Reference != null, "entry.Reference != null");
                    var srcPath = entry.Reference!.Entry.FullName;
                    Log.LogDebug($"{nameof(BackupService)} Opening {srcPath} as src file for diff");
                    var srcEntry = entry.Reference.Entry;
                    using var srcStream = srcEntry!.Open();
                    srcStream.CopyTo(src);
                    srcBuff = src.ToArray();
                }

                using var buff = new MemoryStream();
                BsPatch.Apply(srcBuff, diffBuff, buff);
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None);
                buff.CopyTo(fs);
                return true;
            }
        }

        private IList<BackupRepositoryEntry> Cleanup(BackupRepository repo)
        {
            var res = new List<BackupRepositoryEntry>();
            var stable = false;
            var exceptions = new List<IOException>();
            var now = DateTime.Now;
            while (!stable)
            {
                var expiredEntries = Strategy.ListExpiredEntries(repo, now);
                stable = !expiredEntries.Any();
                foreach (var entry in expiredEntries)
                {
                    try
                    {
                        repo.Delete(entry);
                        Log.LogInfo($"Deleted file \"{entry.RealPath}\"");
                        res.Add(entry);
                    }
                    catch (IOException e)
                    {
                        Log.LogError($"Unable to delete \"{entry.RealPath}\":\n{e.BuildExceptionString()}");
                        exceptions.Add(e);
                    }
                }
            }

            if (!res.Any() && exceptions.Any())
            {
                if (exceptions.Count == 1)
                {
                    throw exceptions.First();
                }

                throw new AggregateException(exceptions);
            }

            return res;
        }

        public void ResetThrottle()
        {
            _saveDiffStopWatch = new Stopwatch();
        }

        public void BackupSave(string path, DateTime? dateTime = null, CancellationToken cancellationToken = default)
        {
            StartBackup(path, dateTime, cancellationToken).Wait(cancellationToken);
        }

        public async Task BackupSaveAsync(string path, DateTime? dateTime = null,
            CancellationToken cancellationToken = default)
        {
            using var timed = new CancellationTokenSource();
            if (Config.Internal.BackupTaskTimeoutTimeSpan > TimeSpan.Zero)
            {
                timed.CancelAfter(Config.Internal.BackupTaskTimeoutTimeSpan);
            }

            using var tokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(Worker.Token, cancellationToken, timed.Token,
                    _cancellationTokenSource.Token);
            tokenSource.Token.ThrowIfCancellationRequested();
            await _saveDiffSemaphore.WaitAsync(tokenSource.Token);

            async Task<T> IoSchedule<T>(Func<T> func)
            {
                // ReSharper disable once AccessToDisposedClosure
                return await IoScheduleFunction(func, tokenSource!.Token);
            }

            async Task IoScheduleAction(Action func)
            {
                object Func()
                {
                    func();
                    return new object();
                }

                await IoSchedule(Func);
            }

            try
            {
                if (ShouldThrottle)
                {
                    Log.LogDebug("Backup Throttle", TraceLevel.Warning);
                    return;
                }

                var now = dateTime ?? DateTime.Now;
                _saveDiffStopWatch = Stopwatch.StartNew();
                var repo = new BackupRepository(BackupUtils.GetRepoFilePath(path));
                var folderName = BackupUtils.GetRepoSubFolderNameForDate(now);
                var oldEntry = Strategy.GetReference(repo, now);
                if (oldEntry is null)
                {
                    Log.LogDebug($"Saving full world into repo \"{path}\"");
                    string referenceName;
                    {
                        using var f = File.OpenRead(path);
                        referenceName = await IoSchedule(() => BackupUtils.HashFile(f));
                    }

                    var fileName = BackupUtils.FormatWorldFileName(now, referenceName);
                    await IoSchedule(() => repo.CreateEntryFromFile(path, $"{folderName}/{fileName}"));
                    Log.LogInfo("Saved world checkpoint");
                }
                else
                {
                    var referenceFileName = BackupUtils.ParseReferenceFileName(oldEntry.Name);
                    Log.LogDebug("Saving diff into repo " + path);
                    var resultPath =
                        $"{folderName}/{BackupUtils.FormatDiffFileName(now, referenceFileName.referenceHash)}";
                    await IoScheduleAction(() => CreateDiff(path, repo, oldEntry, resultPath, now, tokenSource.Token));
                    Log.LogInfo("Saved new world diff");
                }
            }
            finally
            {
                _saveDiffSemaphore.Release();
            }
        }


        private void CreateDiff(string path, BackupRepository repo, BackupRepositoryEntry oldEntry, string resultPath,
            DateTime date,
            CancellationToken cancellationToken)
        {
            byte[] prevBuffer;

            {
                using var s = oldEntry.Open();
                using var ms = new MemoryStream();
                s.CopyTo(ms);
                cancellationToken.ThrowIfCancellationRequested();
                prevBuffer = ms.ToArray();
                Log.LogDebug($"{nameof(BackupService)} has read the old file");
            }
            var diff = Diff(path, prevBuffer);
            cancellationToken.ThrowIfCancellationRequested();
            var entry = repo.CreateEntry(resultPath);
            using (var s = entry.Open(FileMode.Create))
            {
                s.Write(diff, 0, diff.Length);
            }

            entry.LastWriteTime = date;
        }

        private byte[] Diff(string currentFilePath, byte[] oldFileContent)
        {
            using var ms = new MemoryStream();
            var content = File.ReadAllBytes(currentFilePath);
            Worker.Token.ThrowIfCancellationRequested();
            BsDiff.Create(oldFileContent, content, ms);
            return ms.ToArray();
        }


        private async Task<T> IoScheduleFunction<T>(Func<T> action, CancellationToken cancellationToken)
        {
            //check that we have acquired the semaphore before this method call
            if (_saveDiffSemaphore.CurrentCount > 0)
            {
                throw new Exception($"one must acquire and hold {nameof(_saveDiffSemaphore)}");
            }

            return await Worker.IoScheduleFunction(action, cancellationToken);
        }
    }
}