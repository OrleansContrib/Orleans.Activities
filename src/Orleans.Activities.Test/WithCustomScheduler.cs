using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using System.Activities;
using System.Activities.Hosting;
using System.Runtime.DurableInstancing;
using System.Threading;
using System.Threading.Tasks.Schedulers;
using System.Xml.Linq;
using System.Diagnostics;
using Orleans.Activities.Hosting;
using Orleans.Activities.Test.Activities;
using Orleans.Activities.AsyncEx;
using System.IO;
using Orleans.Activities.Persistence;
using System.Activities.Tracking;
using Orleans.Activities.Configuration;

namespace Orleans.Activities.Test
{
    public static class AsyncResetEventExtensions
    {
        public static async Task WaitAsync(this AsyncAutoResetEvent are, double timeoutInSeconds)
        {
            try
            {
                await are.WaitAsync(new CancellationTokenSource(TimeSpan.FromSeconds(timeoutInSeconds)).Token);
            }
            catch (TaskCanceledException)
            {
                throw new TimeoutException();
            }
        }

        public static async Task WaitAsync(this AsyncManualResetEvent mre, double timeoutInSeconds)
        {
            CancellationTokenSource timeoutCts = new CancellationTokenSource();
            Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutInSeconds), timeoutCts.Token);
            await Task.WhenAny(mre.WaitAsync(), timeoutTask);
            if (timeoutTask.IsCompleted)
                throw new TimeoutException();
            timeoutCts.Cancel();
            timeoutTask.Ignore();
        }
    }

    public static class WorkflowHostExtensions
    {
        // Yes, it is a hack, real grain never need this info, this is for double check during tests.
        public static WorkflowInstanceState GetWorkflowInstanceState(this IWorkflowHost workflowHostInterface) =>
            (typeof(WorkflowHost).GetField("instance", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(workflowHostInterface as WorkflowHost) as Hosting.WorkflowInstance).WorkflowInstanceState;
    }

    public class ConsoleTrackingParticipant : Orleans.Activities.Tracking.TrackingParticipant
    {
        protected override Task TrackAsync(TrackingRecord record, TimeSpan timeout)
        {
            Console.WriteLine(record.ToString());
            return TaskConstants.Completed;
        }
    }

    public class WorkflowState : IWorkflowState
    {
        public IDictionary<XName, InstanceValue> InstanceValues { get; set; }
        public WorkflowIdentity WorkflowDefinitionIdentity { get; set; }
    }

    public class Grain : IWorkflowHostCallback, IDisposable
    {
        private Type workflowDefinitionType;

        private List<byte[]> workflowStates;
        private Dictionary<string, TaskTimer> timers;
        public int WorkflowStatesCount => workflowStates.Count;

        private IWorkflowState state;

        public AsyncManualResetEvent UnhandledException { get; private set; }
        public Exception UnhandledExceptionException { get; private set; }
        public AsyncManualResetEvent Completed { get; private set; }
        public ActivityInstanceState CompletionState { get; private set; }
        public IDictionary<string, object> OutputArguments { get; private set; }
        public Exception TerminationException { get; private set; }
        public AsyncManualResetEvent Written { get; private set; }

        public IWorkflowHost WorkflowHost { get; private set; }
        public IWorkflowInstanceCallback WorkflowInstanceCallback { get; private set; }

        public Grain(Type workflowDefinitionType)
            : this(workflowDefinitionType, Guid.NewGuid())
        { }

        public Grain(Type workflowDefinitionType, Guid primaryKey)
        {
            this.workflowDefinitionType = workflowDefinitionType;

            this.workflowStates = new List<byte[]>();
            this.timers = new Dictionary<string, TaskTimer>();

            this.PrimaryKey = primaryKey;
        }

        public void Initialize()
        {
            WorkflowHost wf = new WorkflowHost(this, (wi) => Activator.CreateInstance(workflowDefinitionType) as Activity, null);
            WorkflowHost = wf;
            WorkflowInstanceCallback = wf;

            state = new WorkflowState();

            wf.ExtensionsFactory = () =>
            {
                TrackingProfile trackingProfile = new TrackingProfile();
                trackingProfile.Queries.Add(new WorkflowInstanceQuery()
                {
                    States = { "*" },
                });
                //trackingProfile.Queries.Add(new ActivityScheduledQuery()
                //{
                //    ActivityName = "*",
                //});
                //trackingProfile.Queries.Add(new ActivityStateQuery()
                //{
                //    States = { "*" }
                //});
                trackingProfile.Queries.Add(new BookmarkResumptionQuery()
                {
                    Name = "*",
                });
                trackingProfile.Queries.Add(new CancelRequestedQuery()
                {
                    ActivityName = "*",
                });
                trackingProfile.Queries.Add(new CustomTrackingQuery()
                {
                    ActivityName = "*",
                    Name = "*"
                });
                trackingProfile.Queries.Add(new FaultPropagationQuery()
                {
                    FaultSourceActivityName = "*",
                });

                TrackingParticipant etwTrackingParticipant = new ConsoleTrackingParticipant();
                etwTrackingParticipant.TrackingProfile = trackingProfile;
                return etwTrackingParticipant.Yield();
            };
            wf.OnCompletedAsync = (ActivityInstanceState completionState, IDictionary<string, object> outputArguments, Exception terminationException) =>
            {
                CompletionState = completionState;
                OutputArguments = outputArguments;
                TerminationException = terminationException;
                Completed.Set();
                return TaskConstants.Completed;
            };

            UnhandledException = new AsyncManualResetEvent(false);
            Completed = new AsyncManualResetEvent(false);
            Written = new AsyncManualResetEvent(false);
        }

        public bool throwDuringPersistence;

        public void LoadWorkflowState(int index)
        {
            if (throwDuringPersistence)
            {
                throwDuringPersistence = false;
                throw new TestException("During persistence");
            }
            if (workflowStates.Count == 0 && index == 0)
                state = new WorkflowState();
            else
                state = InstanceValueDictionarySerializer.Deserialize(workflowStates[index]) as IWorkflowState;
        }

        public void Dispose()
        {
            foreach (TaskTimer timer in timers.Values)
                timer.Cancel();
        }

        #region IWorkflowHostCallback members

        public Guid PrimaryKey { get; }

        public IWorkflowState WorkflowState => state;

        public Task LoadWorkflowStateAsync()
        {
            LoadWorkflowState(0);
            return TaskConstants.Completed;
        }

        public Task SaveWorkflowStateAsync()
        {
            if (throwDuringPersistence)
            {
                throwDuringPersistence = false;
                throw new TestException("During persistence");
            }
            workflowStates.Insert(0, InstanceValueDictionarySerializer.Serialize(state));
            Written.Set();
            return TaskConstants.Completed;
        }

        public Task RegisterOrUpdateReminderAsync(string reminderName, TimeSpan dueTime, TimeSpan period)
        {
            TaskTimer timer;
            if (timers.TryGetValue(reminderName, out timer))
                timer.Cancel();
            timers[reminderName] = new TaskTimer(() => WorkflowHost.ReminderAsync(reminderName), dueTime, period);
            return TaskConstants.Completed;
        }

        public Task UnregisterReminderAsync(string reminderName)
        {
            TaskTimer timer;
            if (timers.TryGetValue(reminderName, out timer))
            {
                timer.Cancel();
                timers.Remove(reminderName);
            }
            return TaskConstants.Completed;
        }

        public Task<IEnumerable<string>> GetRemindersAsync() =>
            Task.FromResult(timers.Keys as IEnumerable<string>);

        private IParameters parameters;

        public IParameters Parameters
        {
            get
            {
                if (parameters == null)
                    parameters = new Parameters();
                return parameters;
            }
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));
                //if (parameters != null)
                //    throw new InvalidOperationException(nameof(Parameters) + " property is already set!");
                parameters = value;
            }
        }

        public Task OnUnhandledExceptionAsync(Exception exception, Activity source)
        {
            UnhandledExceptionException = exception;
            UnhandledException.Set();
            return TaskConstants.Completed;
        }

        public Task<Func<Task<TResponseResult>>> OnOperationAsync<TRequestParameter, TResponseResult>(string operationName, TRequestParameter requestParameter)
                where TRequestParameter : class
                where TResponseResult : class =>
            Task.FromResult<Func<Task<TResponseResult>>>(() => Task.FromResult("responseResult" as TResponseResult));

        public Task<Func<Task>> OnOperationAsync<TRequestParameter>(string operationName, TRequestParameter requestParameter)
                where TRequestParameter : class =>
            Task.FromResult<Func<Task>>(() => TaskConstants.Completed);

        public Task<Func<Task<TResponseResult>>> OnOperationAsync<TResponseResult>(string operationName)
                where TResponseResult : class =>
            Task.FromResult<Func<Task<TResponseResult>>>(() => Task.FromResult("responseResult" as TResponseResult));

        public async Task<Func<Task>> OnOperationAsync(string operationName)
        {
            if (operationName.EndsWith("ThrowsBeginAsync"))
                throw new TestException("Begin");
            await Task.Delay(100);
            if (operationName.EndsWith("ThrowsEndAsync"))
                throw new TestException("End");
            return () => TaskConstants.Completed;
        }

        #endregion
    }

    public class MySystemPersistenceParticipant : System.Activities.Persistence.PersistenceParticipant
    {
        protected override void CollectValues(out IDictionary<XName, object> readWriteValues, out IDictionary<XName, object> writeOnlyValues)
        {
            Console.WriteLine("System - CollectValues");
            readWriteValues = null;
            writeOnlyValues = null;
        }

        protected override IDictionary<XName, object> MapValues(IDictionary<XName, object> readWriteValues, IDictionary<XName, object> writeOnlyValues)
        {
            Console.WriteLine("System - MapValues");
            return null;
        }

        protected override void PublishValues(IDictionary<XName, object> readWriteValues)
        {
            Console.WriteLine("System - PublishValues");
        }
    }

    public class MySystemPersistenceIOParticipant : System.Activities.Persistence.PersistenceIOParticipant
    {
        public MySystemPersistenceIOParticipant()
            : base(false, false)
        { }

        protected override IAsyncResult BeginOnSave(IDictionary<XName, object> readWriteValues, IDictionary<XName, object> writeOnlyValues, TimeSpan timeout, AsyncCallback callback, object state) =>
            AsyncFactory.ToBegin(Task.Factory.StartNew(() => Console.WriteLine("System - OnSave")), callback, state);

        protected override void EndOnSave(IAsyncResult result)
        {
            AsyncFactory.ToEnd(result);
        }

        protected override IAsyncResult BeginOnLoad(IDictionary<XName, object> readWriteValues, TimeSpan timeout, AsyncCallback callback, object state) =>
            AsyncFactory.ToBegin(Task.Factory.StartNew(() => Console.WriteLine("System - OnLoad")), callback, state);

        protected override void EndOnLoad(IAsyncResult result)
        {
            AsyncFactory.ToEnd(result);
        }

        protected override void Abort()
        {
            Console.WriteLine("System - Abort");
        }
    }

    public class MyOrleansPersistenceParticipant : Orleans.Activities.Persistence.PersistenceParticipant
    {
        public override void CollectValues(out IDictionary<XName, object> readWriteValues, out IDictionary<XName, object> writeOnlyValues)
        {
            Console.WriteLine("Orleans - CollectValues");
            readWriteValues = null;
            writeOnlyValues = null;
        }

        public override IDictionary<XName, object> MapValues(IDictionary<XName, object> readWriteValues, IDictionary<XName, object> writeOnlyValues)
        {
            Console.WriteLine("Orleans - MapValues");
            return null;
        }

        public override void PublishValues(IDictionary<XName, object> readWriteValues)
        {
            Console.WriteLine("Orleans - PublishValues");
        }
    }

    public class MyOrleansPersistenceIOParticipant : Orleans.Activities.Persistence.PersistenceIOParticipant
    {
        public override Task OnSaveAsync(IDictionary<XName, object> readWriteValues, IDictionary<XName, object> writeOnlyValues, TimeSpan timeout)
        {
            Console.WriteLine("Orleans - OnSave");
            return TaskConstants.Completed;
        }

        public override Task OnSavedAsync(TimeSpan timeout)
        {
            Console.WriteLine("Orleans - OnSaved");
            return TaskConstants.Completed;
        }

        public override Task OnLoadAsync(IDictionary<XName, object> readWriteValues, TimeSpan timeout)
        {
            Console.WriteLine("Orleans - OnLoad");
            return TaskConstants.Completed;
        }

        public override void Abort()
        {
            Console.WriteLine("Orleans - Abort");
        }
    }

    [TestClass]
    public class WithCustomScheduler
    {
        private TaskScheduler taskScheduler;

        [TestInitialize]
        public void TestInitialize()
        {
            // this is the equivalent of the reentrant Orleans scheduler
            taskScheduler = new LimitedConcurrencyLevelTaskScheduler(1);
        }

        [TestCleanup]
        public void TestCleanup()
        {
        }

        private Task RunAsyncWithReentrantSingleThreadedScheduler(Func<Task> function) =>
            Task.Factory.StartNew(function, CancellationToken.None, TaskCreationOptions.None, taskScheduler).Unwrap();

        [TestMethod]
        public async Task SchedulerSingleThreadedness()
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            await RunAsyncWithReentrantSingleThreadedScheduler(async () =>
            {
                Task t1 = Task.Factory.StartNew(async () =>
                {
                    await Task.Delay(100);
                    Thread.Sleep(100);
                }).Unwrap();
                Task t2 = Task.Factory.StartNew(async () =>
                {
                    await Task.Delay(100);
                    Thread.Sleep(100);
                }).Unwrap();
                Task t3 = Task.Factory.StartNew(async () =>
                {
                    await Task.Delay(100);
                    Thread.Sleep(100);
                }).Unwrap();
                await Task.WhenAll(t1, t2, t3);
            });
            stopwatch.Stop();
            Assert.IsTrue(390 <= stopwatch.ElapsedMilliseconds);
        }

        [TestMethod]
        public async Task FromApmEndMethodDoesntEscape()
        {
            await RunAsyncWithReentrantSingleThreadedScheduler(async () =>
            {
                byte[] buffer = new byte[16];
                FileStream stream = new FileStream("Orleans.Activities.dll", FileMode.Open, FileAccess.Read, FileShare.Read, buffer.Length, true);
                int read = 0;

                TaskScheduler schedulerBefore = null;
                TaskScheduler schedulerAfter = null;

                read = await Task.Factory.FromAsync<byte[], int, int, int>(
                    BeginRead((scheduler) => { schedulerBefore = scheduler; }, stream),
                    EndRead((scheduler) => { schedulerAfter = scheduler; }, stream),
                    buffer, 0, buffer.Length, null);
                Assert.AreEqual(16, read);

                Assert.AreEqual(schedulerBefore, taskScheduler);
                Assert.AreEqual(schedulerAfter, TaskScheduler.Default);
                read = await AsyncFactory<int>.FromApm<byte[], int, int>(
                    BeginRead((scheduler) => { schedulerBefore = scheduler; }, stream),
                    EndRead((scheduler) => { schedulerAfter = scheduler; }, stream),
                    buffer, 0, buffer.Length);
                Assert.AreEqual(16, read);

                stream.Close();

                Assert.AreEqual(schedulerBefore, taskScheduler);
                Assert.AreEqual(schedulerAfter, taskScheduler);
                
            });
        }

        private static Func<byte[], int, int, AsyncCallback, object, IAsyncResult> BeginRead(Action<TaskScheduler> setScheduler, FileStream stream) =>
            (byte[] array, int offset, int numBytes, AsyncCallback callback, object state) =>
            {
                setScheduler(TaskScheduler.Current);
                return stream.BeginRead(array, offset, numBytes, callback, state);
            };

        private static Func<IAsyncResult, int> EndRead(Action<TaskScheduler> setScheduler, FileStream stream) =>
            (IAsyncResult asyncResult) =>
            {
                setScheduler(TaskScheduler.Current);
                return stream.EndRead(asyncResult);
            };

        [TestMethod]
        public async Task ResumeBookmarkWithoutPersistence()
        {
            await RunAsyncWithReentrantSingleThreadedScheduler(async () =>
            {
                Grain grain = new Grain(typeof(ResumeBookmarksWithoutPersistence));
                grain.Parameters = new Parameters(idlePersistenceMode: IdlePersistenceMode.Never);
                grain.Initialize();

                Console.WriteLine("RunAsync...");
                await grain.WorkflowHost.RunAsync();

                Console.WriteLine("Delay...");
                // the workflow is gone idle on the delay activity, we must wait to let it to get to the bookmark, to avoid "NotFound"
                await Task.Delay(TimeSpan.FromSeconds(1));

                Console.WriteLine("ResumeBookmarkAsync...");
                BookmarkResumptionResult result = await grain.WorkflowInstanceCallback.ResumeBookmarkAsync(new System.Activities.Bookmark("Bookmark1"), null, TimeSpan.FromSeconds(1));
                Assert.AreEqual(BookmarkResumptionResult.Success, result);

                Console.WriteLine("Completed.WaitAsync...");
                grain.Completed.Reset();
                await grain.Completed.WaitAsync(1);
                Console.WriteLine("---DONE---");
            });
        }

        [TestMethod]
        public async Task ResumeBookmarkWithPersistence()
        {
            await RunAsyncWithReentrantSingleThreadedScheduler(async () =>
            {
                Grain grain = new Grain(typeof(ResumeBookmarksWithPersistence));
                grain.Parameters = new Parameters(idlePersistenceMode: IdlePersistenceMode.Allways);
                grain.Initialize();

                Console.WriteLine("RunAsync... -1/" + grain.WorkflowStatesCount);
                await grain.WorkflowHost.RunAsync();

                Console.WriteLine("Delay...");
                // the workflow is gone idle on the delay activity, we must wait to let it to get to the bookmark, to avoid "NotFound"
                await Task.Delay(TimeSpan.FromSeconds(1));

                Console.WriteLine("ResumeBookmarkAsync Bookmark1...");
                BookmarkResumptionResult result = await grain.WorkflowInstanceCallback.ResumeBookmarkAsync(new System.Activities.Bookmark("Bookmark1"), null, TimeSpan.FromSeconds(1));
                Assert.AreEqual(BookmarkResumptionResult.Success, result);

                Console.WriteLine("ResumeBookmarkAsync Bookmark3...");
                result = await grain.WorkflowInstanceCallback.ResumeBookmarkAsync(new System.Activities.Bookmark("Bookmark3"), null, TimeSpan.FromSeconds(1));
                Assert.AreEqual(BookmarkResumptionResult.Success, result);

                Console.WriteLine("Completed.WaitAsync...");
                grain.Completed.Reset();
                grain.Written.Reset();
                await grain.Completed.WaitAsync(1);
                Console.WriteLine("Written.WaitAsync...");
                await grain.Written.WaitAsync(1);
                Console.WriteLine("---DONE--- " + grain.CompletionState.ToString());
                Assert.AreEqual(5, grain.WorkflowStatesCount);



                Console.WriteLine("Recreate from bookmark3...");
                grain.Initialize();
                grain.LoadWorkflowState(1);
                Console.WriteLine("RunAsync... 1/" + grain.WorkflowStatesCount);
                await grain.WorkflowHost.RunAsync();

                Console.WriteLine("Recreate from bookmark3 again...");
                grain.Initialize();
                grain.LoadWorkflowState(1);
                Console.WriteLine("RunAsync... 1/" + grain.WorkflowStatesCount);
                await grain.WorkflowHost.RunAsync();

                Console.WriteLine("Recreate from runable persist state...");
                grain.Initialize();
                grain.LoadWorkflowState(2);
                await grain.RegisterOrUpdateReminderAsync("{urn:orleans.activities/1.0/properties/reminders}Reactivation", TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(2));
                Console.WriteLine("RunAsync... 2/" + grain.WorkflowStatesCount);
                await grain.WorkflowHost.RunAsync();

                Console.WriteLine("ResumeBookmarkAsync Bookmark3...");
                result = await grain.WorkflowInstanceCallback.ResumeBookmarkAsync(new System.Activities.Bookmark("Bookmark3"), null, TimeSpan.FromSeconds(1));
                Assert.AreEqual(BookmarkResumptionResult.Success, result);

                Console.WriteLine("Completed.WaitAsync...");
                grain.Completed.Reset();
                grain.Written.Reset();
                await grain.Completed.WaitAsync(1);
                Console.WriteLine("Written.WaitAsync...");
                await grain.Written.WaitAsync(1);
                Console.WriteLine("---DONE--- " + grain.CompletionState.ToString());
                Assert.AreEqual(7, grain.WorkflowStatesCount);

                // --------

                grain.Parameters = new Parameters(idlePersistenceMode: IdlePersistenceMode.Allways);
                grain.Initialize();
                Console.WriteLine("RunAsync... -1/" + grain.WorkflowStatesCount);
                await grain.WorkflowHost.RunAsync();

                Console.WriteLine("Delay...");
                // the workflow is gone idle on the delay activity, we must wait to let it to get to the bookmark, to avoid "NotFound"
                await Task.Delay(TimeSpan.FromSeconds(1));

                Console.WriteLine("ResumeBookmarkAsync Bookmark1...");
                result = await grain.WorkflowInstanceCallback.ResumeBookmarkAsync(new System.Activities.Bookmark("Bookmark1"), null, TimeSpan.FromSeconds(1));
                Assert.AreEqual(BookmarkResumptionResult.Success, result);

                Console.WriteLine("ResumeBookmarkAsync Bookmark3...");
                result = await grain.WorkflowInstanceCallback.ResumeBookmarkAsync(new System.Activities.Bookmark("Bookmark3"), null, TimeSpan.FromSeconds(1));
                Assert.AreEqual(BookmarkResumptionResult.Success, result);

                Console.WriteLine("Completed.WaitAsync...");
                grain.Completed.Reset();
                grain.Written.Reset();
                await grain.Completed.WaitAsync(1);
                Console.WriteLine("Written.WaitAsync...");
                await grain.Written.WaitAsync(1);
                Console.WriteLine("---DONE--- " + grain.CompletionState.ToString());
                Assert.AreEqual(12, grain.WorkflowStatesCount);



                Console.WriteLine("Recreate from bookmark3...");
                grain.Initialize();
                grain.LoadWorkflowState(1);
                Console.WriteLine("RunAsync... 1/" + grain.WorkflowStatesCount);
                await grain.WorkflowHost.RunAsync();

                Console.WriteLine("ResumeBookmarkAsync Bookmark3...");
                result = await grain.WorkflowInstanceCallback.ResumeBookmarkAsync(new System.Activities.Bookmark("Bookmark3"), null, TimeSpan.FromSeconds(1));
                Assert.AreEqual(BookmarkResumptionResult.Success, result);

                Console.WriteLine("Completed.WaitAsync...");
                grain.Completed.Reset();
                grain.Written.Reset();
                await grain.Completed.WaitAsync(1);
                Console.WriteLine("Written.WaitAsync...");
                await grain.Written.WaitAsync(1);
                Console.WriteLine("---DONE--- " + grain.CompletionState.ToString());
                Assert.AreEqual(13, grain.WorkflowStatesCount);
            });
        }

        private IEnumerable<object> PersistenceExtensions()
        {
            yield return new MySystemPersistenceParticipant();
            yield return new MySystemPersistenceIOParticipant();
            yield return new MyOrleansPersistenceParticipant();
            yield return new MyOrleansPersistenceIOParticipant();
        }

        [TestMethod]
        public async Task PersistencePipeline()
        {
            await RunAsyncWithReentrantSingleThreadedScheduler(async () =>
            {
                PersistencePipeline pipeline = new PersistencePipeline(PersistenceExtensions(), new Dictionary<XName, InstanceValue>());
                pipeline.Collect();
                pipeline.Map();
                await pipeline.OnSaveAsync(TimeSpan.Zero);
                await pipeline.OnSavedAsync(TimeSpan.Zero);
                await pipeline.OnLoadAsync(TimeSpan.Zero);
                pipeline.Publish();
            });
        }

        [TestMethod]
        public async Task ThrowDuringActivationAndAbort()
        {
            await RunAsyncWithReentrantSingleThreadedScheduler(async () =>
            {
                Grain grain = new Grain(typeof(ThrowDuringActivation));
                grain.Initialize();

                try
                {
                    Console.WriteLine("RunAsync...");
                    await grain.WorkflowHost.RunAsync();
                    Assert.AreEqual(0, grain.WorkflowStatesCount);
                }
                catch (Exception e)
                {
                    Console.WriteLine("...Exception thrown " + e.GetType());
                    Assert.AreEqual(typeof(TestException), e.GetType());
                }
                Console.WriteLine("---DONE---");

                Assert.AreEqual(false, grain.UnhandledException.IsSet);
                Assert.AreEqual(false, grain.Completed.IsSet);
                Assert.AreEqual(false, grain.Written.IsSet);
            });
        }

        [TestMethod]
        public async Task ThrowDuringActivationAndCancel()
        {
            await RunAsyncWithReentrantSingleThreadedScheduler(async () =>
            {
                Grain grain = new Grain(typeof(ThrowDuringActivation));
                grain.Parameters = new Parameters(unhandledExceptionAction: UnhandledExceptionAction.Cancel);
                grain.Initialize();

                try
                {
                    Console.WriteLine("RunAsync...");
                    await grain.WorkflowHost.RunAsync();
                }
                catch (Exception e)
                {
                    Console.WriteLine("...Exception thrown " + e.GetType());
                    Assert.AreEqual(typeof(TestException), e.GetType());
                }
                Console.WriteLine("Completed.WaitAsync...");
                await grain.Completed.WaitAsync(1);
                Console.WriteLine("Written.WaitAsync...");
                await grain.Written.WaitAsync(1);
                Console.WriteLine("---DONE---");

                Assert.AreEqual(ActivityInstanceState.Canceled, grain.CompletionState);
                Assert.AreEqual(null, grain.TerminationException);
                Assert.AreEqual(1, grain.WorkflowStatesCount);
                Assert.AreEqual(false, grain.UnhandledException.IsSet);
            });
        }

        [TestMethod]
        public async Task ThrowDuringActivationAndTerminate()
        {
            await RunAsyncWithReentrantSingleThreadedScheduler(async () =>
            {
                Grain grain = new Grain(typeof(ThrowDuringActivation));
                grain.Parameters = new Parameters(unhandledExceptionAction: UnhandledExceptionAction.Terminate);
                grain.Initialize();

                try
                {
                    Console.WriteLine("RunAsync...");
                    await grain.WorkflowHost.RunAsync();
                }
                catch (Exception e)
                {
                    Console.WriteLine("...Exception thrown " + e.GetType());
                    Assert.AreEqual(typeof(TestException), e.GetType());
                }
                Console.WriteLine("Completed.WaitAsync...");
                await grain.Completed.WaitAsync(1);
                Console.WriteLine("Written.WaitAsync...");
                await grain.Written.WaitAsync(1);
                Console.WriteLine("---DONE---");

                Assert.AreEqual(ActivityInstanceState.Faulted, grain.CompletionState);
                Assert.AreEqual(typeof(TestException), grain.TerminationException.GetType());
                Assert.AreEqual(1, grain.WorkflowStatesCount);
                Assert.AreEqual(false, grain.UnhandledException.IsSet);
            });
        }

        [TestMethod]
        public async Task ThrowUnhandledExceptionAndAbort()
        {
            await RunAsyncWithReentrantSingleThreadedScheduler(async () =>
            {
                Grain grain = new Grain(typeof(ThrowUnhandledException));
                grain.Initialize();

                Console.WriteLine("ResumeBookmarkAsync...");
                BookmarkResumptionResult result = await grain.WorkflowInstanceCallback.ResumeBookmarkAsync(new System.Activities.Bookmark("Bookmark1"), null, TimeSpan.FromSeconds(1));
                Assert.AreEqual(BookmarkResumptionResult.Success, result);

                Console.WriteLine("UnhandledException.WaitAsync...");
                grain.UnhandledException.Reset();
                await grain.UnhandledException.WaitAsync(1);
                Console.WriteLine("---DONE---");
                Assert.AreEqual(typeof(TestException), grain.UnhandledExceptionException.GetType());
                Assert.AreEqual(0, grain.WorkflowStatesCount);
                Assert.AreEqual(false, grain.Completed.IsSet);
                Assert.AreEqual(false, grain.Written.IsSet);
            });
        }

        [TestMethod]
        public async Task ThrowUnhandledExceptionAndCancel()
        {
            await RunAsyncWithReentrantSingleThreadedScheduler(async () =>
            {
                Grain grain = new Grain(typeof(ThrowUnhandledException));
                grain.Parameters = new Parameters(unhandledExceptionAction: UnhandledExceptionAction.Cancel);
                grain.Initialize();

                Console.WriteLine("ResumeBookmarkAsync...");
                BookmarkResumptionResult result = await grain.WorkflowInstanceCallback.ResumeBookmarkAsync(new System.Activities.Bookmark("Bookmark1"), null, TimeSpan.FromSeconds(1));
                Assert.AreEqual(BookmarkResumptionResult.Success, result);

                Console.WriteLine("UnhandledException.WaitAsync...");
                grain.UnhandledException.Reset();
                grain.Completed.Reset();
                grain.Written.Reset();
                await grain.UnhandledException.WaitAsync(1);
                Assert.AreEqual(typeof(TestException), grain.UnhandledExceptionException.GetType());

                Console.WriteLine("Completed.WaitAsync...");
                await grain.Completed.WaitAsync(1);
                Console.WriteLine("Written.WaitAsync...");
                await grain.Written.WaitAsync(1);
                Console.WriteLine("---DONE---");

                Assert.AreEqual(ActivityInstanceState.Canceled, grain.CompletionState);
                Assert.AreEqual(null, grain.TerminationException);
                Assert.AreEqual(1, grain.WorkflowStatesCount);
            });
        }

        [TestMethod]
        public async Task ThrowUnhandledExceptionAndTerminate()
        {
            await RunAsyncWithReentrantSingleThreadedScheduler(async () =>
            {
                Grain grain = new Grain(typeof(ThrowUnhandledException));
                grain.Parameters = new Parameters(unhandledExceptionAction: UnhandledExceptionAction.Terminate);
                grain.Initialize();

                Console.WriteLine("ResumeBookmarkAsync...");
                BookmarkResumptionResult result = await grain.WorkflowInstanceCallback.ResumeBookmarkAsync(new System.Activities.Bookmark("Bookmark1"), null, TimeSpan.FromSeconds(1));
                Assert.AreEqual(BookmarkResumptionResult.Success, result);

                Console.WriteLine("UnhandledException.WaitAsync...");
                grain.UnhandledException.Reset();
                grain.Completed.Reset();
                grain.Written.Reset();
                await grain.UnhandledException.WaitAsync(1);
                Assert.AreEqual(typeof(TestException), grain.UnhandledExceptionException.GetType());

                Console.WriteLine("Completed.WaitAsync...");
                await grain.Completed.WaitAsync(1);
                Console.WriteLine("Written.WaitAsync...");
                await grain.Written.WaitAsync(1);
                Console.WriteLine("---DONE---");

                Assert.AreEqual(ActivityInstanceState.Faulted, grain.CompletionState);
                Assert.AreEqual(typeof(TestException), grain.TerminationException.GetType());
                Assert.AreEqual(1, grain.WorkflowStatesCount);
            });
        }

        [TestMethod]
        public async Task TerminateFromWorkflow()
        {
            await RunAsyncWithReentrantSingleThreadedScheduler(async () =>
            {
                Grain grain = new Grain(typeof(Terminate));
                grain.Initialize();

                Console.WriteLine("RunAsync...");
                await grain.WorkflowHost.RunAsync();

                Console.WriteLine("Completed.WaitAsync...");
                await grain.Completed.WaitAsync(1);
                Console.WriteLine("Written.WaitAsync...");
                await grain.Written.WaitAsync(1);
                Console.WriteLine("---DONE---");

                Assert.AreEqual(ActivityInstanceState.Faulted, grain.CompletionState);
                Assert.AreEqual(typeof(TestException), grain.TerminationException.GetType());
                Assert.AreEqual(1, grain.WorkflowStatesCount);
            });
        }

        [TestMethod]
        public async Task Abort()
        {
            await RunAsyncWithReentrantSingleThreadedScheduler(async () =>
            {
                Grain grain = new Grain(typeof(ThrowUnhandledException));
                grain.Initialize();

                Console.WriteLine("RunAsync...");
                await grain.WorkflowHost.RunAsync();

                Console.WriteLine("AbortAsync...");
                await grain.WorkflowHost.AbortAsync(new TestException());

                Assert.AreEqual(WorkflowInstanceState.Aborted, grain.WorkflowHost.GetWorkflowInstanceState());
                Assert.AreEqual(false, grain.UnhandledException.IsSet);
                Assert.AreEqual(false, grain.Completed.IsSet);
                Assert.AreEqual(false, grain.Written.IsSet);
            });
        }

        [TestMethod]
        public async Task Cancel()
        {
            await RunAsyncWithReentrantSingleThreadedScheduler(async () =>
            {
                Grain grain = new Grain(typeof(ThrowUnhandledException));
                grain.Initialize();

                Console.WriteLine("RunAsync...");
                await grain.WorkflowHost.RunAsync();

                Console.WriteLine("CancelAsync...");
                await grain.WorkflowHost.CancelAsync();

                Console.WriteLine("Completed.WaitAsync...");
                grain.Completed.Reset();
                grain.Written.Reset();
                await grain.Completed.WaitAsync(1);
                Console.WriteLine("Written.WaitAsync...");
                await grain.Written.WaitAsync(1);
                Console.WriteLine("---DONE---");

                Assert.AreEqual(ActivityInstanceState.Canceled, grain.CompletionState);
                Assert.AreEqual(null, grain.TerminationException);
                Assert.AreEqual(1, grain.WorkflowStatesCount);
                Assert.AreEqual(false, grain.UnhandledException.IsSet);
            });
        }

        [TestMethod]
        public async Task Terminate()
        {
            await RunAsyncWithReentrantSingleThreadedScheduler(async () =>
            {
                Grain grain = new Grain(typeof(ThrowUnhandledException));
                grain.Initialize();

                Console.WriteLine("RunAsync...");
                await grain.WorkflowHost.RunAsync();

                Console.WriteLine("TerminateAsync...");
                grain.Completed.Reset();
                grain.Written.Reset();
                await grain.WorkflowHost.TerminateAsync(new TestException());

                Console.WriteLine("Completed.WaitAsync...");
                await grain.Completed.WaitAsync(1);
                Console.WriteLine("Written.WaitAsync...");
                await grain.Written.WaitAsync(1);
                Console.WriteLine("---DONE---");

                Assert.AreEqual(ActivityInstanceState.Faulted, grain.CompletionState);
                Assert.AreEqual(typeof(TestException), grain.TerminationException.GetType());
                Assert.AreEqual(1, grain.WorkflowStatesCount);
                Assert.AreEqual(false, grain.UnhandledException.IsSet);
            });
        }

        [TestMethod]
        public async Task NativeDelay()
        {
            await RunAsyncWithReentrantSingleThreadedScheduler(async () =>
            {
                Grain grain = new Grain(typeof(Orleans.Activities.Test.Activities.TaskAsyncNativeActivity));
                grain.Initialize();

                Console.WriteLine("RunAsync...");
                await grain.WorkflowHost.RunAsync();

                Console.WriteLine("Completed.WaitAsync...");
                await grain.Completed.WaitAsync(2);
                Console.WriteLine("Written.WaitAsync...");
                await grain.Written.WaitAsync(1);
                Console.WriteLine("---DONE---");

                Assert.AreEqual(ActivityInstanceState.Closed, grain.CompletionState);
                //Assert.AreEqual(typeof(TestException), grain.TerminationException.GetType());
                Assert.AreEqual(1, grain.WorkflowStatesCount);
                Assert.AreEqual(false, grain.UnhandledException.IsSet);
            });
        }

        [TestMethod]
        public async Task WorkflowInterfaceOperationWithCancellation()
        {
            await RunAsyncWithReentrantSingleThreadedScheduler(async () =>
            {
                Grain grain = new Grain(typeof(WorkflowInterfaceOperation));
                grain.Initialize();

                try
                {
                    await Task.WhenAll(
                        Task.Factory.StartNew(async () =>
                            {
                                Console.WriteLine("OperationWithoutParamsAsync...");
                                await grain.WorkflowHost.OperationAsync("IWorkflowInterface.OperationWithoutParamsAsync",
                                    () => TaskConstants.Completed);
                                Assert.Fail("OperationWithoutParamsAsync completed");
                            }).Unwrap(),
                        Task.Factory.StartNew(async () =>
                            {
                                Console.WriteLine("OperationWithParamsAsync...");
                                string response = await grain.WorkflowHost.OperationAsync<string, string>("IWorkflowInterface.OperationWithParamsAsync",
                                    () => Task.FromResult("requestResult"));
                                Console.WriteLine("OperationWithParamsAsync completed, response: " + response);
                            }).Unwrap()
                        );
                }
                catch (Exception e)
                {
                    Console.WriteLine("...Exception thrown " + e.GetType());
                    Assert.AreEqual(typeof(TaskCanceledException), e.GetType());
                }
                Console.WriteLine("Completed.WaitAsync...");
                grain.Written.Reset();
                await grain.Completed.WaitAsync(1);
                Console.WriteLine("Written.WaitAsync...");
                await grain.Written.WaitAsync(1);
                Console.WriteLine("---DONE---");

                Assert.AreEqual(ActivityInstanceState.Closed, grain.CompletionState);
                Assert.AreEqual(4, grain.WorkflowStatesCount);
                Assert.AreEqual(false, grain.UnhandledException.IsSet);



                Console.WriteLine("Recreate from persisted idempotent state...");
                grain.Initialize();
                grain.LoadWorkflowState(1);

                Console.WriteLine("OperationAsync...");
                try
                {
                    string response = await grain.WorkflowHost.OperationAsync<string, string>("IWorkflowInterface.OperationWithParamsAsync",
                        () => Task.FromResult("requestResult"));
                    Assert.Fail("OperationWithParamsAsync completed, response: " + response);
                }
                catch (Exception e)
                {
                    Console.WriteLine("...Exception thrown " + e.GetType());
                    Assert.AreEqual(typeof(OperationRepeatedException<string>), e.GetType());
                    Assert.AreEqual("responseParameter", (e as OperationRepeatedException<string>).PreviousResponseParameter);
                }
                try
                {
                    await grain.WorkflowHost.OperationAsync("IWorkflowInterface.OperationWithoutParamsAsync",
                        () => TaskConstants.Completed);
                    Assert.Fail("OperationWithoutParamsAsync completed");
                }
                catch (Exception e)
                {
                    Console.WriteLine("...Exception thrown " + e.GetType());
                    Assert.AreEqual(typeof(OperationCanceledException), e.GetType());
                }
                Console.WriteLine("Completed.WaitAsync...");
                await grain.Completed.WaitAsync(1);
                Console.WriteLine("Written.WaitAsync...");
                await grain.Written.WaitAsync(1);
                Console.WriteLine("---DONE---");

                Assert.AreEqual(ActivityInstanceState.Closed, grain.CompletionState);
                Assert.AreEqual(5, grain.WorkflowStatesCount);
                Assert.AreEqual(false, grain.UnhandledException.IsSet);
            });
        }

        [TestMethod]
        public async Task WorkflowInterfaceOperationWithUnhandledExceptionAndAbort()
        {
            await RunAsyncWithReentrantSingleThreadedScheduler(async () =>
            {
                Grain grain = new Grain(typeof(WorkflowInterfaceOperation));
                grain.Parameters = new Parameters(unhandledExceptionAction: UnhandledExceptionAction.Abort, reactivationReminderPeriod: TimeSpan.FromMilliseconds(500));
                grain.Initialize();

                Console.WriteLine("OperationAsync...");
                try
                {
                    await grain.WorkflowHost.OperationAsync("IWorkflowInterface.OperationWithoutParamsAsync",
                        () => TaskConstants.Completed);
                    Assert.Fail("OperationWithoutParamsAsync completed");
                }
                catch (Exception e)
                {
                    Console.WriteLine("...Exception thrown " + e.GetType());
                    // On first attempt, there is the taskCompletionSource from the operation, the exception is delegated back.
                    Assert.AreEqual(typeof(TestException), e.GetType());
                }

                // Under normal circumstances at this point the exception is already propagated back to the client.
                // Now we don't have anything to await on, no UnhandledException, no Completed, no Written event.
                // Let WF finish the operations after TrySetException().
                await Task.Yield();
                Console.WriteLine("---DONE---");
                Assert.AreEqual(WorkflowInstanceState.Aborted, grain.WorkflowHost.GetWorkflowInstanceState());
                Assert.AreEqual(false, grain.Completed.IsSet);
                Assert.AreEqual(1, grain.WorkflowStatesCount);
                Assert.AreEqual(false, grain.UnhandledException.IsSet);



                Console.WriteLine("Recreate from Delay... (by the repeated reminder)");

                Console.WriteLine("UnhandledException.WaitAsync...");
                // On second attempt, there is no taskCompletionSource from the operation, the exception becomes an UnhandledException.
                await grain.UnhandledException.WaitAsync(1);
                Assert.AreEqual(typeof(TestException), grain.UnhandledExceptionException.GetType());

                // Let WF finish the abortion (we are just after the UnhandledException AsyncManualResetEvent).
                await Task.Yield();
                Console.WriteLine("---DONE---");
                Assert.AreEqual(WorkflowInstanceState.Aborted, grain.WorkflowHost.GetWorkflowInstanceState());
                Assert.AreEqual(1, grain.WorkflowStatesCount);
            });
        }

        [TestMethod]
        public async Task WorkflowInterfaceOperationWithUnhandledExceptionAndTerminate()
        {
            await RunAsyncWithReentrantSingleThreadedScheduler(async () =>
            {
                Grain grain = new Grain(typeof(WorkflowInterfaceOperation));
                grain.Parameters = new Parameters(unhandledExceptionAction: UnhandledExceptionAction.Terminate);
                grain.Initialize();

                Console.WriteLine("OperationAsync...");
                try
                {
                    await grain.WorkflowHost.OperationAsync("IWorkflowInterface.OperationWithoutParamsAsync",
                        () => TaskConstants.Completed);
                    Assert.Fail("OperationWithoutParamsAsync completed");
                }
                catch (Exception e)
                {
                    Console.WriteLine("...Exception thrown " + e.GetType());
                    // On first attempt, there is the taskCompletionSource from the operation, the exception is delegated back.
                    Assert.AreEqual(typeof(TestException), e.GetType());
                }

                Console.WriteLine("Completed.WaitAsync...");
                grain.Written.Reset();
                await grain.Completed.WaitAsync(1);
                Console.WriteLine("Written.WaitAsync...");
                await grain.Written.WaitAsync(1);
                Console.WriteLine("---DONE---");

                Assert.AreEqual(ActivityInstanceState.Faulted, grain.CompletionState);
                Assert.AreEqual(2, grain.WorkflowStatesCount);
                Assert.AreEqual(false, grain.UnhandledException.IsSet);



                // Let WF finish the reminder unregistration (we are just after the Written AsyncManualResetEvent).
                await Task.Yield();
                Console.WriteLine("Recreate from Delay...");
                grain.Initialize();
                grain.LoadWorkflowState(1);
                await grain.RegisterOrUpdateReminderAsync("{urn:orleans.activities/1.0/properties/reminders/bookmarks}1", TimeSpan.FromMilliseconds(500), TimeSpan.FromMinutes(2));

                Console.WriteLine("UnhandledException.WaitAsync...");
                // On second attempt, there is no taskCompletionSource from the operation, the exception becomes an UnhandledException.
                await grain.UnhandledException.WaitAsync(1);
                Assert.AreEqual(typeof(TestException), grain.UnhandledExceptionException.GetType());

                Console.WriteLine("Completed.WaitAsync...");
                grain.Completed.Reset();
                grain.Written.Reset();
                await grain.Completed.WaitAsync(1);
                Console.WriteLine("Written.WaitAsync...");
                await grain.Written.WaitAsync(1);
                Console.WriteLine("---DONE---");
                Assert.AreEqual(ActivityInstanceState.Faulted, grain.CompletionState);
                Assert.AreEqual(3, grain.WorkflowStatesCount);
            });
        }

        [TestMethod]
        public async Task WorkflowInterfaceOperationWithUnhandledExceptionAndCancel()
        {
            await RunAsyncWithReentrantSingleThreadedScheduler(async () =>
            {
                Grain grain = new Grain(typeof(WorkflowInterfaceOperation));
                grain.Parameters = new Parameters(unhandledExceptionAction: UnhandledExceptionAction.Cancel);
                grain.Initialize();

                Console.WriteLine("OperationAsync...");
                try
                {
                    await grain.WorkflowHost.OperationAsync("IWorkflowInterface.OperationWithoutParamsAsync",
                        () => TaskConstants.Completed);
                    Assert.Fail("OperationWithoutParamsAsync completed");
                }
                catch (Exception e)
                {
                    Console.WriteLine("...Exception thrown " + e.GetType());
                    // On first attempt, there is the taskCompletionSource from the operation, the exception is delegated back.
                    Assert.AreEqual(typeof(TestException), e.GetType());
                }

                Console.WriteLine("Completed.WaitAsync...");
                await grain.Completed.WaitAsync(1);
                Console.WriteLine("Written.WaitAsync...");
                grain.Written.Reset();
                await grain.Written.WaitAsync(1);
                Console.WriteLine("---DONE---");

                // TODO CancelWorkflow has one serious flaw: the completion state is Closed and not Canceled.
                Assert.AreEqual(ActivityInstanceState.Closed, grain.CompletionState);
                Assert.AreEqual(3, grain.WorkflowStatesCount);
                Assert.AreEqual(false, grain.UnhandledException.IsSet);



                // Let WF finish the reminder unregistration (we are just after the Written AsyncManualResetEvent).
                await Task.Yield();
                Console.WriteLine("Recreate from Delay...");
                grain.Initialize();
                grain.LoadWorkflowState(2);
                await grain.RegisterOrUpdateReminderAsync("{urn:orleans.activities/1.0/properties/reminders/bookmarks}1", TimeSpan.FromMilliseconds(500), TimeSpan.FromMinutes(2));

                Console.WriteLine("UnhandledException.WaitAsync...");
                // On second attempt, there is no taskCompletionSource from the operation, the exception becomes an UnhandledException.
                await grain.UnhandledException.WaitAsync(1);
                Assert.AreEqual(typeof(TestException), grain.UnhandledExceptionException.GetType());

                Console.WriteLine("Completed.WaitAsync...");
                grain.Completed.Reset();
                await grain.Completed.WaitAsync(1);
                Console.WriteLine("Written.WaitAsync...");
                grain.Written.Reset();
                await grain.Written.WaitAsync(1);
                Console.WriteLine("---DONE---");
                // TODO CancelWorkflow has one serious flaw: the completion state is Closed and not Canceled.
                Assert.AreEqual(ActivityInstanceState.Closed, grain.CompletionState);
                Assert.AreEqual(5, grain.WorkflowStatesCount);
            });
        }

        [TestMethod]
        public async Task WorkflowInterfaceOperationWithExceptionDuringIdempotentPersistence()
        {
            await RunAsyncWithReentrantSingleThreadedScheduler(async () =>
            {
                Grain grain = new Grain(typeof(WorkflowInterfaceOperation));
                grain.Parameters = new Parameters(unhandledExceptionAction: UnhandledExceptionAction.Terminate); // Will not use it, it will abort!
                grain.Initialize();

                grain.throwDuringPersistence = true;
                Console.WriteLine("OperationAsync...");
                try
                {
                    string response = await grain.WorkflowHost.OperationAsync<string, string>("IWorkflowInterface.OperationWithParamsAsync",
                        () => Task.FromResult("requestResult"));
                    Assert.Fail("OperationWithParamsAsync completed, response: " + response);
                }
                catch (Exception e)
                {
                    Console.WriteLine("...Exception thrown " + e.InnerException.GetType());
                    Assert.AreEqual(typeof(TestException), e.InnerException.GetType());
                }

                Console.WriteLine("---DONE---");
                Assert.AreEqual(WorkflowInstanceState.Aborted, grain.WorkflowHost.GetWorkflowInstanceState());
                Assert.AreEqual(false, grain.Completed.IsSet);
                Assert.AreEqual(0, grain.WorkflowStatesCount);
                Assert.AreEqual(false, grain.UnhandledException.IsSet);
            });
        }

        [TestMethod]
        public async Task WorkflowInterfaceOperationWithExceptionDuringImplicitPersistence()
        {
            await RunAsyncWithReentrantSingleThreadedScheduler(async () =>
            {
                Grain grain = new Grain(typeof(WorkflowInterfaceOperation));
                grain.Parameters = new Parameters(unhandledExceptionAction: UnhandledExceptionAction.Terminate); // Will not use it, it will abort!
                grain.Initialize();

                grain.throwDuringPersistence = true;
                Console.WriteLine("OperationAsync...");
                try
                {
                    await grain.WorkflowHost.OperationAsync("IWorkflowInterface.OperationWithoutParamsAsync",
                        () => TaskConstants.Completed);
                    Assert.Fail("OperationWithoutParamsAsync completed");
                }
                catch (Exception e)
                {
                    Console.WriteLine("...Exception thrown " + e.GetType());
                    Assert.AreEqual(typeof(TestException), e.GetType());
                }

                // During operation, the Task throws immediately, not only when the workflow is idle, we have to allow the workflow to get aborted.
                await Task.Yield();
                Console.WriteLine("---DONE---");

                Assert.AreEqual(WorkflowInstanceState.Aborted, grain.WorkflowHost.GetWorkflowInstanceState());
                Assert.AreEqual(false, grain.Completed.IsSet);
                Assert.AreEqual(0, grain.WorkflowStatesCount);
                Assert.AreEqual(false, grain.UnhandledException.IsSet);
            });
        }

        [TestMethod]
        public async Task WorkflowInterfaceOperationWithExceptionDuringExplicitPersistence()
        {
            await RunAsyncWithReentrantSingleThreadedScheduler(async () =>
            {
                Grain grain = new Grain(typeof(ExplicitPersist));
                grain.Parameters = new Parameters(unhandledExceptionAction: UnhandledExceptionAction.Terminate); // Will not use it, it will abort!
                grain.Initialize();

                try
                {
                    grain.throwDuringPersistence = true;
                    Console.WriteLine("RunAsync...");
                    await grain.WorkflowHost.RunAsync();
                }
                catch (Exception e)
                {
                    Console.WriteLine("...Exception thrown " + e.InnerException.GetType());
                    Assert.AreEqual(typeof(TestException), e.InnerException.GetType());
                }

                // During activation, the Task throws only when the workflow is idle, at this time it is already aborted.
                Console.WriteLine("---DONE---");
                Assert.AreEqual(false, grain.Completed.IsSet);
                Assert.AreEqual(0, grain.WorkflowStatesCount);
                Assert.AreEqual(false, grain.UnhandledException.IsSet);
            });
        }

        [TestMethod]
        public async Task WorkflowInterfaceOperationWithExceptionDuringCompletion()
        {
            await RunAsyncWithReentrantSingleThreadedScheduler(async () =>
            {
                Grain grain = new Grain(typeof(ResumeBookmarksWithoutPersistence));
                grain.Parameters = new Parameters(idlePersistenceMode: IdlePersistenceMode.OnCompleted);
                grain.Initialize();

                Console.WriteLine("RunAsync...");
                await grain.WorkflowHost.RunAsync();

                Console.WriteLine("Delay...");
                // the workflow is gone idle on the delay activity, we must wait to let it to get to the bookmark, to avoid "NotFound"
                await Task.Delay(TimeSpan.FromSeconds(1));

                grain.throwDuringPersistence = true;
                Console.WriteLine("ResumeBookmarkAsync...");
                BookmarkResumptionResult result = await grain.WorkflowInstanceCallback.ResumeBookmarkAsync(new System.Activities.Bookmark("Bookmark1"), null, TimeSpan.FromSeconds(1));
                Console.WriteLine("ResumeBookmarkAsync completed");

                Console.WriteLine("UnhandledException.WaitAsync...");
                await grain.Completed.WaitAsync(1);
                await grain.UnhandledException.WaitAsync(1);
                // Let WF abort itself after completed and persistence failed.
                await Task.Yield();
                Console.WriteLine("---DONE---");
                Assert.AreEqual(WorkflowInstanceState.Aborted, grain.WorkflowHost.GetWorkflowInstanceState());
                Assert.AreEqual(0, grain.WorkflowStatesCount);
            });
        }

        [TestMethod]
        public async Task WorkflowInterfaceOperationWithTimeoutWithParallel()
        {
            await RunAsyncWithReentrantSingleThreadedScheduler(async () =>
            {
                Grain grain = new Grain(typeof(WorkflowInterfaceOperationWithTimeoutWithParallel));
                grain.Initialize();

                // It is intentional to call Start, we are testing not calling Operation on time.
                Console.WriteLine("RunAsync...");
                await grain.WorkflowHost.RunAsync();

                Console.WriteLine("Delay...");
                // the workflow is gone idle on the delay activity, we must wait to let it to timeout
                await Task.Delay(TimeSpan.FromSeconds(1));

                try
                {
                    Console.WriteLine("OperationAsync...");
                    await grain.WorkflowHost.OperationAsync("IWorkflowInterface.OperationWithoutParamsAsync",
                        () => TaskConstants.Completed);
                    Assert.Fail("OperationWithoutParamsAsync completed");
                }
                catch (Exception e)
                {
                    Console.WriteLine("...Exception thrown " + e.GetType());
                    Assert.AreEqual(typeof(OperationCanceledException), e.GetType());
                }
                Console.WriteLine("Completed.WaitAsync...");
                await grain.Completed.WaitAsync(1);
                Console.WriteLine("Written.WaitAsync...");
                await grain.Written.WaitAsync(1);
                Console.WriteLine("---DONE---");

                Assert.AreEqual(ActivityInstanceState.Closed, grain.CompletionState);
                Assert.AreEqual(2, grain.WorkflowStatesCount);
                Assert.AreEqual(false, grain.UnhandledException.IsSet);
            });
        }

        [TestMethod]
        public async Task WorkflowInterfaceOperationWithTimeoutWithPick()
        {
            await RunAsyncWithReentrantSingleThreadedScheduler(async () =>
            {
                Grain grain = new Grain(typeof(WorkflowInterfaceOperationWithTimeoutWithPick));
                grain.Initialize();

                // It is intentional to call Start, we are testing not calling Operation on time.
                Console.WriteLine("RunAsync...");
                await grain.WorkflowHost.RunAsync();

                Console.WriteLine("Delay...");
                // the workflow is gone idle on the delay activity, we must wait to let it to timeout
                await Task.Delay(TimeSpan.FromSeconds(1));

                try
                {
                    Console.WriteLine("OperationAsync...");
                    await grain.WorkflowHost.OperationAsync("IWorkflowInterface.OperationWithoutParamsAsync",
                        () => TaskConstants.Completed);
                    Assert.Fail("OperationWithoutParamsAsync completed");
                }
                catch (Exception e)
                {
                    Console.WriteLine("...Exception thrown " + e.GetType());
                    Assert.AreEqual(typeof(OperationCanceledException), e.GetType());
                }
                Console.WriteLine("Completed.WaitAsync...");
                await grain.Completed.WaitAsync(1);
                Console.WriteLine("Written.WaitAsync...");
                await grain.Written.WaitAsync(1);
                Console.WriteLine("---DONE---");

                Assert.AreEqual(ActivityInstanceState.Closed, grain.CompletionState);
                Assert.AreEqual(2, grain.WorkflowStatesCount);
                Assert.AreEqual(false, grain.UnhandledException.IsSet);
            });
        }

        [TestMethod]
        public async Task WorkflowCallbackInterfaceOperation()
        {
            await RunAsyncWithReentrantSingleThreadedScheduler(async () =>
            {
                Grain grain = new Grain(typeof(WorkflowCallbackInterfaceOperation));
                grain.Initialize();

                Console.WriteLine("RunAsync...");
                await grain.WorkflowHost.RunAsync();

                Console.WriteLine("Completed.WaitAsync...");
                await grain.Completed.WaitAsync(1);
                Console.WriteLine("Written.WaitAsync...");
                await grain.Written.WaitAsync(1);
                Console.WriteLine("---DONE---");

                Assert.AreEqual(ActivityInstanceState.Closed, grain.CompletionState);
                Assert.AreEqual(1, grain.WorkflowStatesCount);
                Assert.AreEqual(false, grain.UnhandledException.IsSet);
            });
        }

        [TestMethod]
        public async Task WorkflowCallbackInterfaceOperationWithException()
        {
            await RunAsyncWithReentrantSingleThreadedScheduler(async () =>
            {
                Grain grain = new Grain(typeof(WorkflowCallbackInterfaceOperationWithException));
                grain.Parameters = new Parameters(unhandledExceptionAction: UnhandledExceptionAction.Terminate);
                grain.Initialize();

                Console.WriteLine("RunAsync...");
                await grain.WorkflowHost.RunAsync();

                Console.WriteLine("UnhandledException.WaitAsync...");
                await grain.UnhandledException.WaitAsync(1);
                Console.WriteLine("Completed.WaitAsync...");
                await grain.Completed.WaitAsync(1);
                Console.WriteLine("Written.WaitAsync...");
                await grain.Written.WaitAsync(1);
                Console.WriteLine("---DONE---");
                
                Assert.AreEqual(typeof(TestException), grain.UnhandledExceptionException.GetType());
                Assert.AreEqual(ActivityInstanceState.Faulted, grain.CompletionState);
                Assert.AreEqual(1, grain.WorkflowStatesCount);
            });
        }

        [TestMethod]
        public async Task WorkflowCallbackInterfaceOperationWithThrow()
        {
            await RunAsyncWithReentrantSingleThreadedScheduler(async () =>
            {
                Grain grain = new Grain(typeof(WorkflowCallbackInterfaceOperationWithThrow));
                grain.Parameters = new Parameters(unhandledExceptionAction: UnhandledExceptionAction.Terminate);
                grain.Initialize();

                Console.WriteLine("RunAsync...");
                await grain.WorkflowHost.RunAsync();

                Console.WriteLine("UnhandledException.WaitAsync...");
                await grain.UnhandledException.WaitAsync(1);
                Console.WriteLine("Completed.WaitAsync...");
                await grain.Completed.WaitAsync(1);
                Console.WriteLine("Written.WaitAsync...");
                await grain.Written.WaitAsync(1);
                Console.WriteLine("---DONE---");

                Assert.AreEqual(typeof(TestException), grain.UnhandledExceptionException.GetType());
                Assert.AreEqual(ActivityInstanceState.Faulted, grain.CompletionState);
                Assert.AreEqual(1, grain.WorkflowStatesCount);
            });
        }

        [TestMethod]
        public async Task WorkflowCallbackInterfaceOperationWithCancel()
        {
            await RunAsyncWithReentrantSingleThreadedScheduler(async () =>
            {
                Grain grain = new Grain(typeof(WorkflowCallbackInterfaceOperationWithCancel));
                grain.Parameters = new Parameters(unhandledExceptionAction: UnhandledExceptionAction.Terminate);
                grain.Initialize();

                Console.WriteLine("RunAsync...");
                await grain.WorkflowHost.RunAsync();

                Console.WriteLine("CancelAsync...");
                await grain.WorkflowHost.CancelAsync();

                Console.WriteLine("Completed.WaitAsync...");
                await grain.Completed.WaitAsync(1);
                Console.WriteLine("Written.WaitAsync...");
                await grain.Written.WaitAsync(1);
                Console.WriteLine("---DONE---");

                Assert.AreEqual(ActivityInstanceState.Canceled, grain.CompletionState);
                Assert.AreEqual(1, grain.WorkflowStatesCount);
                Assert.AreEqual(false, grain.UnhandledException.IsSet);
            });
        }

        [TestMethod]
        public async Task WorkflowCallbackInterfaceOperationWithTimeoutWithParallel()
        {
            await RunAsyncWithReentrantSingleThreadedScheduler(async () =>
            {
                Grain grain = new Grain(typeof(WorkflowCallbackInterfaceOperationWithTimeoutWithParallel));
                grain.Initialize();

                Console.WriteLine("RunAsync...");
                await grain.WorkflowHost.RunAsync();

                Console.WriteLine("Completed.WaitAsync...");
                await grain.Completed.WaitAsync(1);
                Console.WriteLine("Written.WaitAsync...");
                await grain.Written.WaitAsync(1);
                Console.WriteLine("---DONE---");

                Assert.AreEqual(ActivityInstanceState.Closed, grain.CompletionState);
                Assert.AreEqual(1, grain.WorkflowStatesCount);
                Assert.AreEqual(false, grain.UnhandledException.IsSet);
            });
        }

        [TestMethod]
        public async Task WorkflowCallbackInterfaceOperationWithTimeoutWithPick()
        {
            await RunAsyncWithReentrantSingleThreadedScheduler(async () =>
            {
                Grain grain = new Grain(typeof(WorkflowCallbackInterfaceOperationWithTimeoutWithPick));
                grain.Initialize();

                Console.WriteLine("RunAsync...");
                await grain.WorkflowHost.RunAsync();

                Console.WriteLine("Completed.WaitAsync...");
                await grain.Completed.WaitAsync(1);
                Console.WriteLine("Written.WaitAsync...");
                await grain.Written.WaitAsync(1);
                Console.WriteLine("---DONE---");

                Assert.AreEqual(ActivityInstanceState.Closed, grain.CompletionState);
                Assert.AreEqual(1, grain.WorkflowStatesCount);
                Assert.AreEqual(false, grain.UnhandledException.IsSet);
            });
        }

        [TestMethod]
        public async Task ParallelIncomingRequests()
        {
            await RunAsyncWithReentrantSingleThreadedScheduler(async () =>
            {
                Grain grain = new Grain(typeof(ParallelIncomingRequests));
                grain.Initialize();

                await Task.WhenAll(
                    Task.Factory.StartNew(async () =>
                    {
                        Console.WriteLine("OperationWithoutParamsAsync...");
                        await grain.WorkflowHost.OperationAsync("IWorkflowInterface.OperationWithoutParamsAsync",
                            () => TaskConstants.Completed);
                        Console.WriteLine("OperationWithoutParamsAsync completed");
                    }).Unwrap(),
                    Task.Factory.StartNew(async () =>
                    {
                        Console.WriteLine("OperationWithParamsAsync...");
                        string response = await grain.WorkflowHost.OperationAsync<string, string>("IWorkflowInterface.OperationWithParamsAsync",
                            () => Task.FromResult("requestResult"));
                        Console.WriteLine("OperationWithParamsAsync completed, response: " + response);
                    }).Unwrap()
                    );
                Console.WriteLine("Completed.WaitAsync...");
                grain.Written.Reset();
                await grain.Completed.WaitAsync(1);
                Console.WriteLine("Written.WaitAsync...");
                await grain.Written.WaitAsync(1);
                Console.WriteLine("---DONE---");

                try
                {
                    string response = await grain.WorkflowHost.OperationAsync<string, string>("IWorkflowInterface.OperationWithParamsAsync",
                        () => Task.FromResult("requestResult"));
                    Console.WriteLine("OperationWithParamsAsync completed, response: " + response);
                }
                catch (Exception e)
                {
                    Console.WriteLine("...Exception thrown " + e.GetType());
                    Assert.AreEqual(typeof(OperationRepeatedException<string>), e.GetType());
                    Assert.AreEqual("Response", (e as OperationRepeatedException<string>).PreviousResponseParameter);
                }
                try
                {
                    await grain.WorkflowHost.OperationAsync("IWorkflowInterface.OperationWithoutParamsAsync",
                        () => TaskConstants.Completed);
                    Console.WriteLine("OperationWithoutParamsAsync completed");
                }
                catch (Exception e)
                {
                    Console.WriteLine("...Exception thrown " + e.GetType());
                    Assert.AreEqual(typeof(OperationRepeatedException), e.GetType());
                }

                Assert.AreEqual(ActivityInstanceState.Closed, grain.CompletionState);
                Assert.AreEqual(6, grain.WorkflowStatesCount);
                Assert.AreEqual(false, grain.UnhandledException.IsSet);



                Console.WriteLine("Recreate from persisted delay state...");
                grain.Initialize();
                grain.LoadWorkflowState(4);
                await grain.RegisterOrUpdateReminderAsync("{urn:orleans.activities/1.0/properties/reminders/bookmarks}1", TimeSpan.FromMilliseconds(500), TimeSpan.FromMinutes(2));
                await grain.RegisterOrUpdateReminderAsync("{urn:orleans.activities/1.0/properties/reminders/bookmarks}2", TimeSpan.FromMilliseconds(500), TimeSpan.FromMinutes(2));

                Console.WriteLine("Completed.WaitAsync...");
                await grain.Completed.WaitAsync(1);
                grain.Written.Reset();
                Console.WriteLine("Written.WaitAsync...");
                await grain.Written.WaitAsync(1);
                Console.WriteLine("---DONE---");

                try
                {
                    string response = await grain.WorkflowHost.OperationAsync<string, string>("IWorkflowInterface.OperationWithParamsAsync",
                        () => Task.FromResult("requestResult"));
                    Console.WriteLine("OperationWithParamsAsync completed, response: " + response);
                }
                catch (Exception e)
                {
                    Console.WriteLine("...Exception thrown " + e.GetType());
                    Assert.AreEqual(typeof(OperationRepeatedException<string>), e.GetType());
                    Assert.AreEqual("Response", (e as OperationRepeatedException<string>).PreviousResponseParameter);
                }
                try
                {
                    await grain.WorkflowHost.OperationAsync("IWorkflowInterface.OperationWithoutParamsAsync",
                        () => TaskConstants.Completed);
                    Console.WriteLine("OperationWithoutParamsAsync completed");
                }
                catch (Exception e)
                {
                    Console.WriteLine("...Exception thrown " + e.GetType());
                    Assert.AreEqual(typeof(OperationRepeatedException), e.GetType());
                }

                Assert.AreEqual(ActivityInstanceState.Closed, grain.CompletionState);
                Assert.AreEqual(10, grain.WorkflowStatesCount);
                Assert.AreEqual(false, grain.UnhandledException.IsSet);
            });
        }

        [TestMethod]
        public async Task CancellationScope()
        {
            await RunAsyncWithReentrantSingleThreadedScheduler(async () =>
            {
                Grain grain = new Grain(typeof(CancellationScope));
                grain.Initialize();

                Console.WriteLine("RunAsync...");
                await grain.WorkflowHost.RunAsync();

                Console.WriteLine("Completed.WaitAsync...");
                await grain.Completed.WaitAsync(1);
                Console.WriteLine("Written.WaitAsync...");
                await grain.Written.WaitAsync(1);
                Console.WriteLine("---DONE---");

                Assert.AreEqual(ActivityInstanceState.Closed, grain.CompletionState);
                Assert.AreEqual(1, grain.WorkflowStatesCount);
                Assert.AreEqual(false, grain.UnhandledException.IsSet);

                Assert.IsNotNull(grain.OutputArguments);
                object result;
                Assert.IsTrue(grain.OutputArguments.TryGetValue("Result", out result));
                Assert.AreEqual("Canceled", (string)result);
            });
        }

        [TestMethod]
        public async Task Compensation()
        {
            await RunAsyncWithReentrantSingleThreadedScheduler(async () =>
            {
                Grain grain = new Grain(typeof(ExceptionCausingCustomCancellation));
                grain.Initialize();

                Console.WriteLine("RunAsync...");
                await grain.WorkflowHost.RunAsync();

                Console.WriteLine("Completed.WaitAsync...");
                await grain.Completed.WaitAsync(1);
                Console.WriteLine("Written.WaitAsync...");
                await grain.Written.WaitAsync(1);
                Console.WriteLine("---DONE---");

                Assert.AreEqual(ActivityInstanceState.Closed, grain.CompletionState);
                Assert.AreEqual(1, grain.WorkflowStatesCount);
                Assert.AreEqual(false, grain.UnhandledException.IsSet);

                Assert.IsNotNull(grain.OutputArguments);
                object result;
                Assert.IsTrue(grain.OutputArguments.TryGetValue("Result", out result));
                Assert.AreEqual("Cancellation C;Cancellation;Confirmation A;Compensation B;Exception catched;", (string)result);



                grain = new Grain(typeof(CancellingActivityInvokesCustomCancellation));
                grain.Initialize();

                Console.WriteLine("RunAsync...");
                await grain.WorkflowHost.RunAsync();

                Console.WriteLine("Completed.WaitAsync...");
                await grain.Completed.WaitAsync(1);
                Console.WriteLine("Written.WaitAsync...");
                await grain.Written.WaitAsync(1);
                Console.WriteLine("---DONE---");

                Assert.AreEqual(ActivityInstanceState.Closed, grain.CompletionState);
                Assert.AreEqual(1, grain.WorkflowStatesCount);
                Assert.AreEqual(false, grain.UnhandledException.IsSet);

                Assert.IsNotNull(grain.OutputArguments);
                Assert.IsTrue(grain.OutputArguments.TryGetValue("Result", out result));
                Assert.AreEqual("Cancellation C;Cancellation;Confirmation B;Confirmation A;", (string)result);
            });
        }
    }
}
