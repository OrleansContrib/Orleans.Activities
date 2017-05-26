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
    /// WorkflowHost throws the OperationRepeatedException with the help of it.
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
        protected abstract class ResponseParameter
        {
            public bool IsCanceled { get; }
            public Type Type { get; }
            public object Value { get; }

            protected ResponseParameter(bool isCanceled, Type type, object value)
            {
                IsCanceled = isCanceled;
                Type = type;
                Value = value;
            }
        }

        [Serializable]
        protected class CanceledResponseParameter : ResponseParameter
        {
            public CanceledResponseParameter()
                : base(true, null, null)
            { }
        }

        [Serializable]
        protected class SentResponseParameter : ResponseParameter
        {
            public SentResponseParameter(Type type, object value)
                : base(false, type, value)
            { }
        }

        protected Dictionary<string, ResponseParameter> previousResponseParameters;

        public PreviousResponseParameterExtension()
        {
            previousResponseParameters = new Dictionary<string, ResponseParameter>();
        }

        // Called by ReceiveRequestSendResponseScope activity.
        public bool TrySetResponseCanceled(string operationName)
        {
            bool containsKey = previousResponseParameters.ContainsKey(operationName);
            if (!containsKey)
                previousResponseParameters[operationName] = new CanceledResponseParameter();
            return !containsKey;
        }

        // Called by SendResponse activity.
        public void SetResponseParameter(string operationName, Type type, object value)
        {
            previousResponseParameters[operationName] = new SentResponseParameter(type, value);
        }

        // Called by WorkflowHost.
        public Exception CreatePreviousResponseParameterException<TResponseParameter>(string operationName, Type responseParameterType)
        {
            if (!previousResponseParameters.TryGetValue(operationName, out ResponseParameter previousResponseParameter))
                return new InvalidOperationException($"Operation '{operationName}' is unexpected.");
            if (previousResponseParameter.IsCanceled)
                return new OperationCanceledException($"Operation '{operationName}' is already canceled.");
            if (previousResponseParameter.Type != responseParameterType)
                return new ArgumentException($"Operation '{operationName}' has different ResponseParameter type '{responseParameterType}' then the previous operation '{previousResponseParameter.Type}' had.");
            string message = $"Operation '{operationName}' is already executed.";
            if (responseParameterType == typeof(void))
                return new OperationRepeatedException(message);
            else
                return new OperationRepeatedException<TResponseParameter>((TResponseParameter)previousResponseParameter.Value, message);
        }

        #region PersistenceParticipant members

        public override void CollectValues(out IDictionary<XName, object> readWriteValues, out IDictionary<XName, object> writeOnlyValues)
        {
            readWriteValues = null;
            writeOnlyValues = null;

            if (previousResponseParameters.Count > 0)
            {
                readWriteValues = new Dictionary<XName, object>(1) {{ WorkflowNamespace.PreviousResponseParameters, previousResponseParameters }};
            }
        }

        public override void PublishValues(IDictionary<XName, object> readWriteValues)
        {
            this.previousResponseParameters.Clear();

            if (readWriteValues != null
                && readWriteValues.TryGetValue(WorkflowNamespace.PreviousResponseParameters, out object previousResponseParameters)
                && previousResponseParameters is Dictionary<string, ResponseParameter>)
                this.previousResponseParameters = previousResponseParameters as Dictionary<string, ResponseParameter>;
        }

        #endregion
    }
}
