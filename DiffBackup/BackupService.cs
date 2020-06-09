using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using deltaq_tie.BsDiff;
using ThreadState = System.Threading.ThreadState;

#nullable enable

namespace DiffBackup
{
    public interface IBackupService
    {
        Task StartBackup(string path, CancellationToken cancellationToken);
        void BackupSave(string path, CancellationToken cancellationToken);
        Task BackupSaveAsync(string path, CancellationToken cancellationToken);
        void Dispose();
    }

    public class BackupService : IDisposable, IBackupService
    {
        public readonly ITlog Log;
        private Stopwatch _saveDiffStopWatch = new Stopwatch();
        private readonly SemaphoreSlim _saveDiffSemaphore = new SemaphoreSlim(1, 1);

        private bool ShouldThrottle =>
            _saveDiffStopWatch.IsRunning && _saveDiffStopWatch.Elapsed < TimeSpan.FromMinutes(3);

        private readonly Thread _ioThread;

        private Action? _ioScheduledAction;
        private readonly ManualResetEventSlim _ioEvent = new ManualResetEventSlim();
        private readonly CancellationTokenSource _ioStop = new CancellationTokenSource();

        public BackupService(ITlog log)
        {
            Log = log;
            _ioThread = new Thread(o => ThreadRoutine((Barrier) o)) {Priority = ThreadPriority.Lowest};
        }

        public Task StartBackup(string path, CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew(async () => await BackupSaveAsync(path, cancellationToken), cancellationToken);
        }

        public void BackupSave(string path, CancellationToken cancellationToken)
        {
            StartBackup(path, cancellationToken).Wait(cancellationToken);
        }

        public async Task BackupSaveAsync(string path, CancellationToken cancellationToken)
        {
            using var timed = new CancellationTokenSource();
            timed.CancelAfter(TimeSpan.FromMinutes(3));
            using var tokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(_ioStop.Token, cancellationToken, timed.Token);
            tokenSource.Token.ThrowIfCancellationRequested();
            await _saveDiffSemaphore.WaitAsync(tokenSource.Token);

            Task<T> IoSchedule<T>(Func<T> func)
            {
                return IoScheduleFunction(func, tokenSource!.Token);
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

                var now = DateTime.Now;
                _saveDiffStopWatch = Stopwatch.StartNew();
                var fileName = Path.GetFileName(path);
                using var zip = await IoSchedule(() => ZipFile.Open(path + ".backup.zip", ZipArchiveMode.Update));
                var folderName = now.ToString("yyyy_MM_dd");
                var diffSrcPath = folderName + "/" + fileName;
                var oldEntry = zip.GetEntry(diffSrcPath);
                if (oldEntry is null)
                {
                    Log.LogDebug("Saving full world into zip " + path);
                    await IoSchedule(() => zip.CreateEntryFromFile(path, diffSrcPath, CompressionLevel.Optimal));
                    Log.LogInfo("Saved world checkpoint");
                }
                else
                {
                    Log.LogDebug("Saving diff into zip " + path, TraceLevel.Info);
                    var resultPath = $"{folderName}/{now:HH_mm_ss}.bsdiff";
                    await IoScheduleAction(() => CreateDiff(path, zip, oldEntry, resultPath, tokenSource.Token));
                    Log.LogInfo("Saved new world diff");
                }
            }
            finally
            {
                _saveDiffSemaphore.Release();
            }
        }

        private void CreateDiff(string path, ZipArchive zip, ZipArchiveEntry oldEntry, string resultPath,
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
            var entry = zip.CreateEntry(resultPath, CompressionLevel.Fastest);
            using (var s = entry.Open())
            {
                s.Write(diff, 0, diff.Length);
            }
        }

        private byte[] Diff(string currentFilePath, byte[] oldFileContent)
        {
            using var ms = new MemoryStream();
            var content = File.ReadAllBytes(currentFilePath);
            _ioStop.Token.ThrowIfCancellationRequested();
            BsDiff.Create(oldFileContent, content, ms);
            return ms.ToArray();
        }


        private async Task<T> IoScheduleFunction<T>(Func<T> action, CancellationToken cancellationToken)
        {
            TryStartThread(cancellationToken);

            Debug.Assert(_ioScheduledAction is null, nameof(_ioScheduledAction) + " is not null");

            //check that we have acquired the semaphore before this method call
            if (_saveDiffSemaphore.CurrentCount > 0)
            {
                throw new Exception($"one must acquire and hold {nameof(_saveDiffSemaphore)}");
            }

            var tcs = new TaskCompletionSource<T>();
            _ioScheduledAction = () =>
            {
                try
                {
                    var result = action();
                    tcs.SetResult(result);
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
            };


            _ioEvent.Set();
            try
            {
                return await tcs.Task;
            }
            finally
            {
                _ioScheduledAction = null;
                _ioEvent.Reset();
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        private bool TryStartThread(CancellationToken cancellationToken)
        {
            if (_ioThread.ThreadState != ThreadState.Unstarted) return false;
            using var barrier = new Barrier(2);
            _ioThread.Start(barrier);
            barrier.SignalAndWait(cancellationToken);
            return true;
        }

        private void ThreadRoutine(Barrier barrier)
        {
            barrier.SignalAndWait(_ioStop.Token);
            Log.LogDebug($"{nameof(BackupService)} Thread Start");
            while (!_ioStop.IsCancellationRequested)
            {
                try
                {
                    _ioEvent.Wait(_ioStop.Token);
                }
                catch (OperationCanceledException ex)
                {
                    Log.LogDebug(ex.StackTrace, TraceLevel.Error);
                    if (_ioStop.IsCancellationRequested) break;
                    throw;
                }

                Debug.Assert(_ioScheduledAction != null, nameof(_ioScheduledAction) + " == null");
                _ioScheduledAction!();
            }

            Log.LogDebug($"{nameof(BackupService)} Thread Stop");
        }

        public void Dispose()
        {
            Log.LogDebug($"{nameof(BackupService)} Dispose");
            _ioStop.Cancel(false);
            if (!_ioThread.Join(5000))
            {
                _ioThread.Abort();
            }

            _ioEvent.Dispose();
            _saveDiffSemaphore.Dispose();
            _ioStop.Dispose();
        }
    }
}