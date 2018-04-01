using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Orleans.Activities.AsyncEx;

namespace Orleans.Activities.Persistence
{
    /// <summary>
    /// Extension methods compiled at static run time to access the internal <see cref="System.Runtime.IPersistencePipelineModule"/> members.
    /// These are the methods that are implemented by the abstract <see cref="System.Activities.Persistence.PersistenceIOParticipant"/> class.
    /// Legacy persistence extensions implement these methods. It also contains TAP async wrapper methods for the same functionality.
    /// <para>The reimplemented <see cref="PersistencePipeline"/> calls these methods.</para>
    /// </summary>
    public static class PersistenceIOParticipantExtensions
    {
        private delegate IAsyncResult BeginOnSaveDelegate(System.Activities.Persistence.PersistenceIOParticipant persistenceIOParticipant, IDictionary<XName, object> readWriteValues, IDictionary<XName, object> writeOnlyValues, TimeSpan timeout, AsyncCallback callback, object state);
        private delegate void EndOnSaveDelegate(System.Activities.Persistence.PersistenceIOParticipant persistenceIOParticipant, IAsyncResult result);
        private delegate IAsyncResult BeginOnLoadDelegate(System.Activities.Persistence.PersistenceIOParticipant persistenceIOParticipant, IDictionary<XName, object> readWriteValues, TimeSpan timeout, AsyncCallback callback, object state);
        private delegate void EndOnLoadDelegate(System.Activities.Persistence.PersistenceIOParticipant persistenceIOParticipant, IAsyncResult result);
        private delegate void AbortDelegate(System.Activities.Persistence.PersistenceIOParticipant persistenceIOParticipant);

        private static BeginOnSaveDelegate beginOnSave;
        private static EndOnSaveDelegate endOnSave;
        private static BeginOnLoadDelegate beginOnLoad;
        private static EndOnLoadDelegate endOnLoad;
        private static AbortDelegate abort;

        static PersistenceIOParticipantExtensions()
        {
#pragma warning disable IDE0007 // Use implicit type (https://github.com/dotnet/roslyn/issues/766)
            ParameterExpression instance = Expression.Parameter(typeof(System.Activities.Persistence.PersistenceIOParticipant), "this");
            ParameterExpression readWriteValues = Expression.Parameter(typeof(IDictionary<XName, object>), nameof(readWriteValues));
            ParameterExpression writeOnlyValues = Expression.Parameter(typeof(IDictionary<XName, object>), nameof(writeOnlyValues));
            ParameterExpression timeout = Expression.Parameter(typeof(TimeSpan), nameof(timeout));
            ParameterExpression callback = Expression.Parameter(typeof(AsyncCallback), nameof(callback));
            ParameterExpression state = Expression.Parameter(typeof(object), nameof(state));
            ParameterExpression result = Expression.Parameter(typeof(IAsyncResult), nameof(result));
            MethodInfo method;
#pragma warning restore IDE0007 // Use implicit type

            method = typeof(System.Activities.Persistence.PersistenceIOParticipant).GetMethod(
                PersistenceParticipantExtensions.IPersistencePipelineModuleFullName + nameof(BeginOnSave), BindingFlags.Instance | BindingFlags.NonPublic);
            beginOnSave = Expression.Lambda<BeginOnSaveDelegate>(
                Expression.Call(instance, method, readWriteValues, writeOnlyValues, timeout, callback, state), true, instance, readWriteValues, writeOnlyValues, timeout, callback, state).Compile();

            method = typeof(System.Activities.Persistence.PersistenceIOParticipant).GetMethod(
                PersistenceParticipantExtensions.IPersistencePipelineModuleFullName + nameof(EndOnSave), BindingFlags.Instance | BindingFlags.NonPublic);
            endOnSave = Expression.Lambda<EndOnSaveDelegate>(
                Expression.Call(instance, method, result), true, instance, result).Compile();

            method = typeof(System.Activities.Persistence.PersistenceIOParticipant).GetMethod(
                PersistenceParticipantExtensions.IPersistencePipelineModuleFullName + nameof(BeginOnLoad), BindingFlags.Instance | BindingFlags.NonPublic);
            beginOnLoad = Expression.Lambda<BeginOnLoadDelegate>(
                Expression.Call(instance, method, readWriteValues, timeout, callback, state), true, instance, readWriteValues, timeout, callback, state).Compile();

            method = typeof(System.Activities.Persistence.PersistenceIOParticipant).GetMethod(
                PersistenceParticipantExtensions.IPersistencePipelineModuleFullName + nameof(EndOnLoad), BindingFlags.Instance | BindingFlags.NonPublic);
            endOnLoad = Expression.Lambda<EndOnLoadDelegate>(
                Expression.Call(instance, method, result), true, instance, result).Compile();

            method = typeof(System.Activities.Persistence.PersistenceIOParticipant).GetMethod(
                PersistenceParticipantExtensions.IPersistencePipelineModuleFullName + nameof(Abort), BindingFlags.Instance | BindingFlags.NonPublic);
            abort = Expression.Lambda<AbortDelegate>(
                Expression.Call(instance, method), true, instance).Compile();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static IAsyncResult BeginOnSave(this System.Activities.Persistence.PersistenceIOParticipant persistenceIOParticipant,
                IDictionary<XName, object> readWriteValues, IDictionary<XName, object> writeOnlyValues, TimeSpan timeout,
                AsyncCallback callback, object state)
            => beginOnSave(persistenceIOParticipant, readWriteValues, writeOnlyValues, timeout, callback, state);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EndOnSave(this System.Activities.Persistence.PersistenceIOParticipant persistenceIOParticipant, IAsyncResult result)
            => endOnSave(persistenceIOParticipant, result);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task OnSaveAsync(this System.Activities.Persistence.PersistenceIOParticipant persistenceIOParticipant,
                IDictionary<XName, object> readWriteValues, IDictionary<XName, object> writeOnlyValues, TimeSpan timeout)
            => AsyncFactory.FromApm<IDictionary<XName, object>, IDictionary<XName, object>, TimeSpan>(
                persistenceIOParticipant.BeginOnSave, persistenceIOParticipant.EndOnSave,
                readWriteValues, writeOnlyValues, timeout);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static IAsyncResult BeginOnLoad(this System.Activities.Persistence.PersistenceIOParticipant persistenceIOParticipant,
                IDictionary<XName, object> readWriteValues, TimeSpan timeout,
                AsyncCallback callback, object state)
            => beginOnLoad(persistenceIOParticipant, readWriteValues, timeout, callback, state);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EndOnLoad(this System.Activities.Persistence.PersistenceIOParticipant persistenceIOParticipant, IAsyncResult result)
            => endOnLoad(persistenceIOParticipant, result);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task OnLoadAsync(this System.Activities.Persistence.PersistenceIOParticipant persistenceIOParticipant,
                IDictionary<XName, object> readWriteValues, TimeSpan timeout)
            => AsyncFactory.FromApm<IDictionary<XName, object>, TimeSpan>(
                persistenceIOParticipant.BeginOnLoad, persistenceIOParticipant.EndOnLoad,
                readWriteValues, timeout);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Abort(this System.Activities.Persistence.PersistenceIOParticipant persistenceIOParticipant)
            => abort(persistenceIOParticipant);
    }
}
