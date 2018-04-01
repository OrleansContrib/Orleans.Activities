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
    public class OperationRepeatedException : SystemException
    {
        public OperationRepeatedException()
        { }

        public OperationRepeatedException(string message)
            : base(message)
        { }

        public OperationRepeatedException(string message, Exception innerException)
            : base(message, innerException)
        { }

        protected OperationRepeatedException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        { }
    }

    /// <summary>
    /// The exception that is thrown when an idempotent operation is already executed by the workflow.
    /// It also contains the previous return value (response parameter) of the already executed operation.
    /// </summary>
    /// <typeparam name="TPreviousResponseParameter"></typeparam>
    [Serializable]
    public class OperationRepeatedException<TPreviousResponseParameter> : OperationRepeatedException
    {
        private TPreviousResponseParameter previousResponseParameter;

        public TPreviousResponseParameter PreviousResponseParameter
            => this.previousResponseParameter;

        private void Init(TPreviousResponseParameter previousResponseParameter)
            => this.previousResponseParameter = previousResponseParameter;

        public OperationRepeatedException(TPreviousResponseParameter previousResponseParameter)
            => Init(previousResponseParameter);

        public OperationRepeatedException(TPreviousResponseParameter previousResponseParameter, string message)
            : base(message)
            => Init(previousResponseParameter);

        public OperationRepeatedException(TPreviousResponseParameter previousResponseParameter, string message, Exception innerException)
            : base(message, innerException)
            => Init(previousResponseParameter);

        protected OperationRepeatedException(SerializationInfo info, StreamingContext context)
            : base(info, context)
            => previousResponseParameter = (TPreviousResponseParameter)info.GetValue(nameof(previousResponseParameter), typeof(TPreviousResponseParameter));

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(this.previousResponseParameter), this.previousResponseParameter);
        }
    }
}
