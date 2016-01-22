using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;
using System.Xml.Linq;
using Orleans.Activities.Persistence;

namespace Orleans.Activities.Extensions
{
    public static class PreviousResponseParameterExtensionExtensions
    {
        public static PreviousResponseParameterExtension GetPreviousResponseParameterExtension(this ActivityContext context)
        {
            PreviousResponseParameterExtension previousResponseParameterExtension = context.GetExtension<PreviousResponseParameterExtension>();
            if (previousResponseParameterExtension == null)
                throw new ValidationException(nameof(PreviousResponseParameterExtension) + " is not found.");
            return previousResponseParameterExtension;
        }
    }

    /// <summary>
    /// This extension is always created by the workflow host.
    /// It participates in the workflow persistence, and SendResponse activity stores the idempotent TWorkflowInterface operation responses in it.
    /// WorkflowHost throws the RepeatedOperationException with the help of it.
    /// </summary>
    public class PreviousResponseParameterExtension : PersistenceParticipant
    {
        public static class WorkflowNamespace
        {
            private static readonly XNamespace responsesPath = XNamespace.Get(Persistence.WorkflowNamespace.BaseNamespace + "/responses");
            private static readonly XName previousResponseParameters = responsesPath.GetName(nameof(PreviousResponseParameters));
            public static XName PreviousResponseParameters => previousResponseParameters;
        }

        [Serializable]
        protected struct ResponseParameter
        {
            public Type type;
            public object value;

            public ResponseParameter(Type type, object value)
            {
                this.type = type;
                this.value = value;
            }
        }

        protected Dictionary<string, ResponseParameter> previousResponseParameters;

        public PreviousResponseParameterExtension()
        {
            previousResponseParameters = new Dictionary<string, ResponseParameter>();
        }

        // Called by SendResponse activity.
        public void SetResponseParameter(string operationName, Type type, object value)
        {
            previousResponseParameters[operationName] = new ResponseParameter(type, value);
        }

        // Called by WorkflowHost.
        public void ThrowPreviousResponseParameter(string operationName, Type responseParameterType,
            Func<object, RepeatedOperationException> createRepeatedOperationException)
        {
            ResponseParameter previousResponseParameter;
            if (!previousResponseParameters.TryGetValue(operationName, out previousResponseParameter))
                throw new InvalidOperationException($"Operation '{operationName}' is unexpected.");
            if (previousResponseParameter.type != responseParameterType)
                throw new ArgumentException($"Operation '{operationName}' has different ResponseParameter type '{responseParameterType}' then the previous operation '{previousResponseParameter.type}' had.");
            throw createRepeatedOperationException(previousResponseParameter.value);
        }

        #region PersistenceParticipant members

        public override void CollectValues(out IDictionary<XName, object> readWriteValues, out IDictionary<XName, object> writeOnlyValues)
        {
            readWriteValues = null;
            writeOnlyValues = null;

            if (previousResponseParameters.Count > 0)
            {
                readWriteValues = new Dictionary<XName, object>(1);
                readWriteValues.Add(WorkflowNamespace.PreviousResponseParameters, previousResponseParameters);
            }
        }

        public override void PublishValues(IDictionary<XName, object> readWriteValues)
        {
            this.previousResponseParameters.Clear();

            object previousResponseParameters;
            if (readWriteValues != null
                && readWriteValues.TryGetValue(WorkflowNamespace.PreviousResponseParameters, out previousResponseParameters)
                && previousResponseParameters is Dictionary<string, ResponseParameter>)
                this.previousResponseParameters = previousResponseParameters as Dictionary<string, ResponseParameter>;
        }

        #endregion
    }
}
