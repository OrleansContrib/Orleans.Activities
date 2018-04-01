using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace Orleans.Activities.Persistence
{
    /// <summary>
    /// Extension methods compiled at static run time to access the internal <see cref="System.Runtime.IPersistencePipelineModule"/> members.
    /// These are the methods that are implemented by the abstract <see cref="System.Activities.Persistence.PersistenceParticipant"/> class.
    /// Legacy persistence extensions implement these methods. It also contains TAP async wrapper methods for the same functionality.
    /// <para>The reimplemented <see cref="PersistencePipeline"/> calls these methods.</para>
    /// </summary>
    public static class PersistenceParticipantExtensions
    {
        private delegate bool IsIOParticipantDelegate(System.Activities.Persistence.PersistenceParticipant persistenceParticipant);
        private delegate void CollectValuesDelegate(System.Activities.Persistence.PersistenceParticipant persistenceParticipant, out IDictionary<XName, object> readWriteValues, out IDictionary<XName, object> writeOnlyValues);
        private delegate IDictionary<XName, object> MapValuesDelegate(System.Activities.Persistence.PersistenceParticipant persistenceParticipant, IDictionary<XName, object> readWriteValues, IDictionary<XName, object> writeOnlyValues);
        private delegate void PublishValuesDelegate(System.Activities.Persistence.PersistenceParticipant persistenceParticipant, IDictionary<XName, object> readWriteValues);

        internal const string IPersistencePipelineModuleFullName = "System.Runtime.IPersistencePipelineModule.";

        private static IsIOParticipantDelegate isIOParticipant;
        private static CollectValuesDelegate collectValues;
        private static MapValuesDelegate mapValues;
        private static PublishValuesDelegate publishValues;

        static PersistenceParticipantExtensions()
        {
#pragma warning disable IDE0007 // Use implicit type (https://github.com/dotnet/roslyn/issues/766)
            ParameterExpression instance = Expression.Parameter(typeof(System.Activities.Persistence.PersistenceParticipant), "this");
            ParameterExpression readWriteValues = Expression.Parameter(typeof(IDictionary<XName, object>), nameof(readWriteValues));
            ParameterExpression outReadWriteValues = Expression.Parameter(typeof(IDictionary<XName, object>).MakeByRefType(), nameof(readWriteValues));
            ParameterExpression writeOnlyValues = Expression.Parameter(typeof(IDictionary<XName, object>), nameof(writeOnlyValues));
            ParameterExpression outWriteOnlyValues = Expression.Parameter(typeof(IDictionary<XName, object>).MakeByRefType(), nameof(writeOnlyValues));
            MethodInfo method;
#pragma warning restore IDE0007 // Use implicit type

            method = typeof(System.Activities.Persistence.PersistenceParticipant).GetProperty(
                IPersistencePipelineModuleFullName + nameof(IsIOParticipant), BindingFlags.Instance | BindingFlags.NonPublic).GetMethod;
            isIOParticipant = Expression.Lambda<IsIOParticipantDelegate>(
                Expression.Call(instance, method), true, instance).Compile();

            method = typeof(System.Activities.Persistence.PersistenceParticipant).GetMethod(
                IPersistencePipelineModuleFullName + nameof(CollectValues), BindingFlags.Instance | BindingFlags.NonPublic);
            collectValues = Expression.Lambda<CollectValuesDelegate>(
                Expression.Call(instance, method, outReadWriteValues, outWriteOnlyValues), true, instance, outReadWriteValues, outWriteOnlyValues).Compile();

            method = typeof(System.Activities.Persistence.PersistenceParticipant).GetMethod(
                IPersistencePipelineModuleFullName + nameof(MapValues), BindingFlags.Instance | BindingFlags.NonPublic);
            mapValues = Expression.Lambda<MapValuesDelegate>(
                Expression.Call(instance, method, readWriteValues, writeOnlyValues), true, instance, readWriteValues, writeOnlyValues).Compile();

            method = typeof(System.Activities.Persistence.PersistenceParticipant).GetMethod(
                IPersistencePipelineModuleFullName + nameof(PublishValues), BindingFlags.Instance | BindingFlags.NonPublic);
            publishValues = Expression.Lambda<PublishValuesDelegate>(
                Expression.Call(instance, method, readWriteValues), true, instance, readWriteValues).Compile();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsIOParticipant(this System.Activities.Persistence.PersistenceParticipant persistenceParticipant)
            => isIOParticipant(persistenceParticipant);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CollectValues(this System.Activities.Persistence.PersistenceParticipant persistenceParticipant,
                out IDictionary<XName, object> readWriteValues, out IDictionary<XName, object> writeOnlyValues)
            => collectValues(persistenceParticipant, out readWriteValues, out writeOnlyValues);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IDictionary<XName, object> MapValues(this System.Activities.Persistence.PersistenceParticipant persistenceParticipant,
                IDictionary<XName, object> readWriteValues, IDictionary<XName, object> writeOnlyValues)
            => mapValues(persistenceParticipant, readWriteValues, writeOnlyValues);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void PublishValues(this System.Activities.Persistence.PersistenceParticipant persistenceParticipant,
                IDictionary<XName, object> readWriteValues)
            => publishValues(persistenceParticipant, readWriteValues);
    }
}
