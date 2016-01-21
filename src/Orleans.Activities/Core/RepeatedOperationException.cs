using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Runtime.Serialization;

namespace Orleans.Activities
{
    /// <summary>
    /// The exception that is thrown when an idempotent operation is already executed by the workflow.
    /// </summary>
    [Serializable]
    public class RepeatedOperationException : InvalidOperationException
    {
        public RepeatedOperationException()
        { }

        public RepeatedOperationException(string message)
            : base(message)
        { }

        public RepeatedOperationException(string message, Exception innerException)
            : base(message, innerException)
        { }

        protected RepeatedOperationException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        { }
    }

    /// <summary>
    /// The exception that is thrown when an idempotent operation is already executed by the workflow.
    /// It also contains the previous return value (response parameter) of the already executed operation.
    /// </summary>
    /// <typeparam name="TPreviousResponseParameter"></typeparam>
    [Serializable]
    public class RepeatedOperationException<TPreviousResponseParameter> : RepeatedOperationException
        where TPreviousResponseParameter : class
    {
        private TPreviousResponseParameter previousResponseParameter;

        public TPreviousResponseParameter PreviousResponseParameter => previousResponseParameter;

        private void Init(TPreviousResponseParameter previousResponseParameter)
        {
            this.previousResponseParameter = previousResponseParameter;
        }

        public RepeatedOperationException(TPreviousResponseParameter previousResponseParameter)
        {
            Init(previousResponseParameter);
        }

        public RepeatedOperationException(TPreviousResponseParameter previousResponseParameter, string message)
            : base(message)
        {
            Init(previousResponseParameter);
        }

        public RepeatedOperationException(TPreviousResponseParameter previousResponseParameter, string message, Exception innerException)
            : base(message, innerException)
        {
            Init(previousResponseParameter);
        }

        protected RepeatedOperationException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            previousResponseParameter = (TPreviousResponseParameter)info.GetValue(nameof(previousResponseParameter), typeof(TPreviousResponseParameter));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);

            info.AddValue(nameof(previousResponseParameter), previousResponseParameter);
        }
    }
}
