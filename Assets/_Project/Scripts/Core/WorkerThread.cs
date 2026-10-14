using System;
using System.Threading;

namespace Cinderbird.Core
{
    /// <summary>
    /// One long-lived background thread that runs the same piece of work each time it's started, for work handed
    /// off many times a second. Unlike Task.Run it allocates nothing per run, so it adds no per-frame garbage.
    /// Start, then check <see cref="IsCompleted"/> or <see cref="Wait"/>; one run at a time. Dispose when done.
    /// </summary>
    public sealed class WorkerThread : IDisposable
    {
        private readonly Func<bool> _work;
        private readonly Thread _thread;
        // Signalling both ways is a full memory barrier, so fields set before Start are seen by the work, and fields
        // the work sets are seen after Wait.
        private readonly AutoResetEvent _startSignal = new AutoResetEvent(false);
        private readonly ManualResetEventSlim _doneSignal = new ManualResetEventSlim(true);
        private volatile bool _stopping;
        private bool _result;
        private Exception _error;

        /// <param name="work">Runs on the worker thread; its result comes back from <see cref="Wait"/>.</param>
        public WorkerThread(string name, Func<bool> work)
        {
            _work = work;
            // Background: never keeps the game from quitting.
            _thread = new Thread(Loop) { Name = name, IsBackground = true };
            _thread.Start();
        }

        /// <summary>Whether the last run has finished (true before the first run).</summary>
        public bool IsCompleted => _doneSignal.IsSet;

        /// <summary>Runs the work once on the worker. The previous run must have been collected with <see cref="Wait"/>.</summary>
        public void Start()
        {
            if (!_doneSignal.IsSet)
            {
                throw new InvalidOperationException("The worker is still running.");
            }

            _doneSignal.Reset();
            _startSignal.Set();
        }

        /// <summary>Waits for the current run and returns its result; <paramref name="error"/> is set if it threw.</summary>
        public bool Wait(out Exception error)
        {
            _doneSignal.Wait();
            error = _error;
            _error = null;
            return error == null && _result;
        }

        public void Dispose()
        {
            _stopping = true;
            _startSignal.Set();
            // A run in progress finishes first; give it a moment rather than block quitting forever.
            if (_thread.Join(1000))
            {
                _startSignal.Dispose();
                _doneSignal.Dispose();
            }
        }

        private void Loop()
        {
            while (true)
            {
                _startSignal.WaitOne();
                if (_stopping)
                {
                    return;
                }

                try
                {
                    _result = _work();
                }
                catch (Exception e)
                {
                    _result = false;
                    _error = e;
                }
                finally
                {
                    _doneSignal.Set();
                }
            }
        }
    }
}
