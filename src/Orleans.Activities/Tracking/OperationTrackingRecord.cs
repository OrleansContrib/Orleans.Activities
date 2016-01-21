using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities.Tracking;

namespace Orleans.Activities.Tracking
{
    /// <summary>
    /// Abstract base class for tracking operations.
    /// </summary>
    public abstract class OperationTrackingRecord : CustomTrackingRecord
    {
        protected OperationTrackingRecord(string name)
            : base(name)
        { }
    }

    /// <summary>
    /// Abstract base class for tracking operation requests.
    /// </summary>
    public abstract class OperationRequestTrackingRecord : OperationTrackingRecord
    {
        private const string OperationName = "OperationName";

        protected OperationRequestTrackingRecord(string name, string operationName)
            : base(name)
        {
            if (string.IsNullOrEmpty(operationName))
                throw new ArgumentNullException(nameof(operationName));
            Data.Add(OperationName, operationName);
        }

        public override string ToString() =>
            $"{Name} {{ InstanceId = {InstanceId}, RecordNumber = {RecordNumber}, EventTime = {EventTime}, Operation = {Data[OperationName] as string}, Activity {{ {Activity?.ToString() ?? "<null>"} }} }}";
    }

    /// <summary>
    /// Abstract base class for tracking operation responses.
    /// </summary>
    public abstract class OperationResponseTrackingRecord : CustomTrackingRecord
    {
        protected OperationResponseTrackingRecord(string name)
            : base(name)
        { }

        public override string ToString() =>
            $"{Name} {{ InstanceId = {InstanceId}, RecordNumber = {RecordNumber}, EventTime = {EventTime}, Activity {{ {Activity?.ToString() ?? "<null>"} }} }}";
    }

    /// <summary>
    /// Tracking record for SendRequest activities.
    /// </summary>
    public sealed class SendRequestRecord : OperationRequestTrackingRecord
    {
        private static readonly string RecordName = nameof(SendRequestRecord);
        private const string RequestParameter = "RequestParameter";

        public SendRequestRecord(string operationName)
            : base(SendRequestRecord.RecordName, operationName)
        { }

        public SendRequestRecord(string operationName, object requestParameter)
            : this(operationName)
        {
            Data.Add(RequestParameter, requestParameter);
        }
    }

    /// <summary>
    /// Tracking record for ReceiveResponse activities.
    /// </summary>
    public sealed class ReceiveResponseRecord : OperationResponseTrackingRecord
    {
        private static readonly string RecordName = nameof(ReceiveResponseRecord);
        private const string ResponseResult = "ResponseResult";

        public ReceiveResponseRecord()
            : base(ReceiveResponseRecord.RecordName)
        { }

        public ReceiveResponseRecord(object responseResult)
            : this()
        {
            Data.Add(ResponseResult, responseResult);
        }
    }

    /// <summary>
    /// Tracking record for ReceiveRequest activities.
    /// </summary>
    public sealed class ReceiveRequestRecord : OperationRequestTrackingRecord
    {
        private static readonly string RecordName = nameof(ReceiveRequestRecord);
        private const string RequestResult = "RequestResult";

        public ReceiveRequestRecord(string operationName)
            : base(ReceiveRequestRecord.RecordName, operationName)
        { }

        public ReceiveRequestRecord(string operationName, object requestResult)
            : this(operationName)
        {
            Data.Add(RequestResult, requestResult);
        }
    }

    /// <summary>
    /// Tracking record for SendResponse activities.
    /// </summary>
    public sealed class SendResponseRecord : OperationResponseTrackingRecord
    {
        private static readonly string RecordName = nameof(SendResponseRecord);
        private const string ResponseParameter = "ResponseParameter";

        public SendResponseRecord()
            : base(SendResponseRecord.RecordName)
        { }

        public SendResponseRecord(object responseParameter)
            : this()
        {
            Data.Add(ResponseParameter, responseParameter);
        }
    }
}
