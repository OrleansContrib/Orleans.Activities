
Original source: https://github.com/StephenCleary/AsyncEx

Used APIs:
 - AsyncAutoResetEvent
 - AsyncManualResetEvent
 - AsyncFactory (FromApm, ToBegin, ToEnd only)
 - TaskConstants

Main modifications:
- Scheduler and continuations:
  - TaskScheduler.Current is used instead of TaskScheduler.Default in:
    - AsyncAutoResetEvent.WaitAsync()/AsyncWaitQueue.Enqueue()
    - AsyncFactory.ToBegin()
  - useSynchronizationContext: true is used instead of useSynchronizationContext: false in:
    - AsyncAutoResetEvent.WaitAsync()/AsyncWaitQueue.Enqueue()
  - TaskContinuationOptions.ExecuteSynchronously is used in:
    - AsyncFactory.ToBegin()
  - TrySetResult is used instead of TrySetResultWithBackgroundContinuations() in:
    - AsyncManualResetEvent.Set()
  - Execute endMethod under the same scheduler as beginMethod in FromApm() methods.
- Synchronously completed tasks:
  - Verify asyncResult.CompletedSynchronously in FromApm() methods.
  - Use a new AsyncResultCompletedSynchronously wrapper for synchronously completed tasks in ToBegin()/ToEnd().
- No locks used (no need for it in a grain under Orleans scheduler) in:
  - AsyncAutoResetEvent
  - AsyncManualResetEvent
  - AsyncWaitQueue

Additional modifications:
- Copied method implementations from large classes when only used at a few places:
  - TaskCompletionSourceExtensions.TryCompleteFromCompletedTask()
  - TaskExtensions.WaitAndUnwrapException()
  - TaskShim.FromResult()
- Non-generic TaskCompletionSource is removed.
