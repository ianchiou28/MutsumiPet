using System;
using System.Collections.Generic;
using System.Windows.Threading;

namespace MutsumiPet.Support
{
    /// Stands in for the cancellable `Task { try? await Task.sleep(...) }` pattern the
    /// macOS store uses for bubble dismissal and activity timeouts. Disposing the
    /// handle cancels the pending callback.
    public interface IPetScheduler
    {
        IDisposable Schedule(TimeSpan delay, Action action);
    }

    public sealed class DispatcherPetScheduler : IPetScheduler
    {
        private readonly Dispatcher dispatcher;

        public DispatcherPetScheduler() : this(Dispatcher.CurrentDispatcher)
        {
        }

        public DispatcherPetScheduler(Dispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
        }

        public IDisposable Schedule(TimeSpan delay, Action action)
        {
            return new ScheduledWork(dispatcher, delay, action);
        }

        private sealed class ScheduledWork : IDisposable
        {
            private readonly DispatcherTimer timer;

            public ScheduledWork(Dispatcher dispatcher, TimeSpan delay, Action action)
            {
                timer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher);
                timer.Interval = delay;
                timer.Tick += delegate
                {
                    timer.Stop();
                    action();
                };
                timer.Start();
            }

            public void Dispose()
            {
                timer.Stop();
            }
        }
    }

    /// Test scheduler: records pending callbacks and only runs them when asked, so
    /// store behaviour can be asserted without waiting on wall-clock time.
    public sealed class ManualPetScheduler : IPetScheduler
    {
        private readonly List<PendingWork> pending = new List<PendingWork>();

        public int PendingCount
        {
            get { return pending.Count; }
        }

        public IDisposable Schedule(TimeSpan delay, Action action)
        {
            var work = new PendingWork(this, delay, action);
            pending.Add(work);
            return work;
        }

        /// Runs every callback scheduled with exactly this delay, oldest first.
        public void Fire(TimeSpan delay)
        {
            var due = new List<PendingWork>();
            foreach (PendingWork work in pending)
            {
                if (work.Delay == delay) due.Add(work);
            }

            foreach (PendingWork work in due)
            {
                pending.Remove(work);
                work.Run();
            }
        }

        private sealed class PendingWork : IDisposable
        {
            private readonly ManualPetScheduler owner;
            private readonly Action action;

            public readonly TimeSpan Delay;

            public PendingWork(ManualPetScheduler owner, TimeSpan delay, Action action)
            {
                this.owner = owner;
                this.action = action;
                Delay = delay;
            }

            public void Run()
            {
                action();
            }

            public void Dispose()
            {
                owner.pending.Remove(this);
            }
        }
    }
}
