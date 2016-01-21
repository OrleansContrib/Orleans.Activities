// original source https://github.com/StephenCleary/AsyncEx

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Diagnostics;
using System.Threading;

// Original idea by Stephen Toub: http://blogs.msdn.com/b/pfxteam/archive/2012/02/11/10266923.aspx

namespace Orleans.Activities.AsyncEx
{
    // MODIFIED
    /// <summary>
    /// An async-compatible auto-reset event.
    /// <para>Can be used only inside reentrant grains, not thread safe, no race conditions are handled!</para>
    /// </summary>
    [DebuggerDisplay("Id = {Id}, IsSet = {_set}")]
    [DebuggerTypeProxy(typeof(DebugView))]
    public sealed class AsyncAutoResetEvent
    {
        /// <summary>
        /// The queue of TCSs that other tasks are awaiting.
        /// </summary>
        private readonly IAsyncWaitQueue<object> _queue;

        /// <summary>
        /// The current state of the event.
        /// </summary>
        private bool _set;

        /// <summary>
        /// The semi-unique identifier for this instance. This is 0 if the id has not yet been created.
        /// </summary>
        private int _id;

        // MODIFIED
        ///// <summary>
        ///// The object used for mutual exclusion.
        ///// </summary>
        //private readonly object _mutex;

        /// <summary>
        /// Creates an async-compatible auto-reset event.
        /// </summary>
        /// <param name="set">Whether the auto-reset event is initially set or unset.</param>
        /// <param name="queue">The wait queue used to manage waiters.</param>
        public AsyncAutoResetEvent(bool set, IAsyncWaitQueue<object> queue)
        {
            _queue = queue;
            _set = set;
            // MODIFIED
            //_mutex = new object();
            //if (set)
            //    Enlightenment.Trace.AsyncAutoResetEvent_Set(this);
        }

        /// <summary>
        /// Creates an async-compatible auto-reset event.
        /// </summary>
        /// <param name="set">Whether the auto-reset event is initially set or unset.</param>
        public AsyncAutoResetEvent(bool set)
            : this(set, new DefaultAsyncWaitQueue<object>())
        { }

        /// <summary>
        /// Creates an async-compatible auto-reset event that is initially unset.
        /// </summary>
        public AsyncAutoResetEvent()
          : this(false, new DefaultAsyncWaitQueue<object>())
        { }

        /// <summary>
        /// Gets a semi-unique identifier for this asynchronous auto-reset event.
        /// </summary>
        public int Id =>
            IdManager<AsyncAutoResetEvent>.GetId(ref _id);

        /// <summary>
        /// Whether this event is currently set. This member is seldom used; code using this member has a high possibility of race conditions.
        /// </summary>
        public bool IsSet =>
            // MODIFIED
            //get { lock (_mutex) return _set; }
            _set;

        /// <summary>
        /// Asynchronously waits for this event to be set. If the event is set, this method will auto-reset it and return immediately, even if the cancellation token is already signalled. If the wait is canceled, then it will not auto-reset this event.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token used to cancel this wait.</param>
        public Task WaitAsync(CancellationToken cancellationToken)
        {
            Task ret;
            // MODIFIED
            //lock (_mutex)
            //{
                if (_set)
                {
                    _set = false;
                    ret = TaskConstants.Completed;
                }
                else
                {
                    // MODIFIED
                    //ret = _queue.Enqueue(_mutex, cancellationToken);
                    ret = _queue.Enqueue(cancellationToken);
                }
                //Enlightenment.Trace.AsyncAutoResetEvent_TrackWait(this, ret);
            //}

            return ret;
        }

        // MODIFIED
        ///// <summary>
        ///// Synchronously waits for this event to be set. If the event is set, this method will auto-reset it and return immediately, even if the cancellation token is already signalled. If the wait is canceled, then it will not auto-reset this event. This method may block the calling thread.
        ///// </summary>
        ///// <param name="cancellationToken">The cancellation token used to cancel this wait.</param>
        //public void Wait(CancellationToken cancellationToken)
        //{
        //    Task ret;
        //    // MODIFIED
        //    //lock (_mutex)
        //    //{
        //        if (_set)
        //        {
        //            _set = false;
        //            return;
        //        }
        //
        //        // MODIFIED
        //        //ret = _queue.Enqueue(_mutex, cancellationToken);
        //        ret = _queue.Enqueue(cancellationToken);
        //    //}
        //
        //    // MODIFIED
        //    //ret.WaitAndUnwrapException();
        //    ret.GetAwaiter().GetResult();
        //}

        /// <summary>
        /// Asynchronously waits for this event to be set. If the event is set, this method will auto-reset it and return immediately.
        /// </summary>
        public Task WaitAsync() =>
            WaitAsync(CancellationToken.None);

        // MODIFIED
        ///// <summary>
        ///// Synchronously waits for this event to be set. If the event is set, this method will auto-reset it and return immediately. This method may block the calling thread.
        ///// </summary>
        //public void Wait()
        //{
        //    Wait(CancellationToken.None);
        //}

        /// <summary>
        /// Sets the event, atomically completing a task returned by <see cref="o:WaitAsync"/>. If the event is already set, this method does nothing.
        /// </summary>
        public void Set()
        {
            IDisposable finish = null;
            // MODIFIED
            //lock (_mutex)
            //{
                //Enlightenment.Trace.AsyncAutoResetEvent_Set(this);
                if (_queue.IsEmpty)
                    _set = true;
                else
                    finish = _queue.Dequeue();
            //}
            if (finish != null)
                finish.Dispose();
        }

        // ReSharper disable UnusedMember.Local
        [DebuggerNonUserCode]
        private sealed class DebugView
        {
            private readonly AsyncAutoResetEvent _are;

            public DebugView(AsyncAutoResetEvent are)
            {
                _are = are;
            }

            public int Id => _are.Id;

            public bool IsSet => _are._set;

            public IAsyncWaitQueue<object> WaitQueue => _are._queue;
        }
        // ReSharper restore UnusedMember.Local
    }
}
