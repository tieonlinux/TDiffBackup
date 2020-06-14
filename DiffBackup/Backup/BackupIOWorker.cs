using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ThreadState = System.Threading.ThreadState;

#nullable enable

namespace DiffBackup.Backup
{
    public class BackupIOWorker : IDisposable
    {
        private readonly Thread _ioThread;

        public readonly ITlog Log;

        private Action? _ioScheduledAction = null;
        private readonly SemaphoreSlim _ioSemaphore = new SemaphoreSlim(1, 1);
        private readonly ManualResetEventSlim _ioEvent = new ManualResetEventSlim();
        private readonly CancellationTokenSource _ioStop = new CancellationTokenSource();

        public CancellationToken Token => _ioStop.Token;

        public BackupIOWorker(ITlog log)
        {
            Log = log;
            _ioThread = new Thread(o => ThreadRoutine((Barrier) o)) {Priority = ThreadPriority.Lowest};
        }

        public async Task<T> IoScheduleFunction<T>(Func<T> action, CancellationToken cancellationToken)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cancellationToken = cts.Token;
            await _ioSemaphore.WaitAsync(cancellationToken);
            var taskLaunched = false;
            try
            {
                if (TryStartThread(cancellationToken))
                {
                    Log.LogDebug($"{nameof(BackupIOWorker)} IO Thread started");
                }
                Debug.Assert(_ioScheduledAction is null, nameof(_ioScheduledAction) + " is null");
                var tcs = new TaskCompletionSource<T>();
                _ioScheduledAction = () =>
                {
                    T result;
                    try
                    {
                        result = action();
                    }
                    catch (OperationCanceledException)
                    {
                        tcs.TrySetCanceled(cancellationToken);
                        return;
                    }
                    catch (Exception e)
                    {
                        tcs.TrySetException(e);
                        return;
                    }

                    tcs.SetResult(result);
                };

                _ioEvent.Set();
                taskLaunched = true;
                return await tcs.Task;
            }
            finally
            {
                if (!taskLaunched)
                {
                    _ioSemaphore.Release();
                }
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
            Log.LogDebug($"{nameof(BackupIOWorker)} Thread Start");
            while (!_ioStop.IsCancellationRequested)
            {
                try
                {
                    _ioEvent.Wait(_ioStop.Token);
                }
                catch (OperationCanceledException)
                {
                    if (_ioStop.IsCancellationRequested) break;
                    throw;
                }

                try
                {
                    _ioScheduledAction?.Invoke();
                }
                finally
                {
                    _ioScheduledAction = null;
                    _ioEvent.Reset();
                    _ioSemaphore.Release();
                }
            }

            Log.LogDebug($"{nameof(BackupIOWorker)} Thread Stop");
        }

        public void Dispose()
        {
            Log.LogDebug($"{nameof(BackupIOWorker)} Dispose");
            _ioStop.Cancel(false);
            if (_ioThread.IsAlive)
            {
                if (!_ioThread.Join(5000))
                {
                    _ioThread.Abort();
                }
            }

            _ioEvent.Dispose();
            _ioStop.Dispose();
        }
    }
}