using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities.Tracking;
using System.Runtime.Serialization;

namespace Orleans.Activities.Tracking
{
    /// <summary>
    /// Abstract base class for tracking operations.
    /// </summary>
    [DataContract]
    public abstract class OperationTrackingRecord : CustomTrackingRecord
    {
        protected OperationTrackingRecord(string name)
            : base(name)
        { }
    }

    /// <summary>
    /// Abstract base class for tracking operation requests.
    /// </summary>
    [DataContract]
    public abstract class OperationRequestTrackingRecord : OperationTrackingRecord
    {
        private const string OperationNameKey = "OperationName";

        protected OperationRequestTrackingRecord(string name, string operationName)
            : base(name)
        {
            if (string.IsNullOrEmpty(operationName))
                throw new ArgumentNullException(nameof(operationName));
            this.OperationName = operationName;
        }

        [DataMember]
        public string OperationName
        {
            get => this.Data.TryGetValue(OperationNameKey, out var operationName) ? operationName as string : string.Empty;
            private set => this.Data[OperationNameKey] = value;
        }

        public override string ToString()
            => $"{this.Name} {{ InstanceId = {this.InstanceId}, RecordNumber = {this.RecordNumber}, EventTime = {this.EventTime}, Operation = {this.OperationName}, Activity {{ {this.Activity?.ToString() ?? "<null>"} }} }}";
    }

    /// <summary>
    /// Abstract base class for tracking operation responses.
    /// </summary>
    [DataContract]
    public abstract class OperationResponseTrackingRecord : OperationTrackingRecord
    {
        protected OperationResponseTrackingRecord(string name)
            : base(name)
        { }

        public override string ToString()
            => $"{this.Name} {{ InstanceId = {this.InstanceId}, RecordNumber = {this.RecordNumber}, EventTime = {this.EventTime}, Activity {{ {this.Activity?.ToString() ?? "<null>"} }} }}";
    }

    /// <summary>
    /// Tracking record for SendRequest activities.
    /// </summary>
    [DataContract]
    public sealed class SendRequestRecord : OperationRequestTrackingRecord
    {
        private const string RecordName = nameof(SendRequestRecord);
        private const string RequestParameterKey = "RequestParameter";

        public SendRequestRecord(string operationName)
            : base(SendRequestRecord.RecordName, operationName)
        { }

        public SendRequestRecord(string operationName, object requestParameter)
            : this(operationName)
            => this.RequestParameter = requestParameter;

        public object RequestParameter
        {
            get => this.Data.TryGetValue(RequestParameterKey, out var requestParameter) ? requestParameter : null;
            private set => this.Data[RequestParameterKey] = value;
        }
    }

    /// <summary>
    /// Tracking record for ReceiveResponse activities.
    /// </summary>
    [DataContract]
    public sealed class ReceiveResponseRecord : OperationResponseTrackingRecord
    {
        private const string RecordName = nameof(ReceiveResponseRecord);
        private const string ResponseResultKey = "ResponseResult";

        public ReceiveResponseRecord()
            : base(ReceiveResponseRecord.RecordName)
        { }

        public ReceiveResponseRecord(object responseResult)
            : this()
            => this.ResponseResult = responseResult;

        public object ResponseResult
        {
            get => this.Data.TryGetValue(ResponseResultKey, out var responseResult) ? responseResult : null;
            private set => this.Data[ResponseResultKey] = value;
        }
    }

    /// <summary>
    /// Tracking record for ReceiveRequest activities.
    /// </summary>
    [DataContract]
    public sealed class ReceiveRequestRecord : OperationRequestTrackingRecord
    {
        private const string RecordName = nameof(ReceiveRequestRecord);
        private const string RequestResultKey = "RequestResult";

        public ReceiveRequestRecord(string operationName)
            : base(ReceiveRequestRecord.RecordName, operationName)
        { }

        public ReceiveRequestRecord(string operationName, object requestResult)
            : this(operationName)
            => this.RequestResult = requestResult;

        public object RequestResult
        {
            get => this.Data.TryGetValue(RequestResultKey, out var requestResult) ? requestResult : null;
            private set => this.Data[RequestResultKey] = value;
        }
    }

    /// <summary>
    /// Tracking record for SendResponse activities.
    /// </summary>
    [DataContract]
    public sealed class SendResponseRecord : OperationResponseTrackingRecord
    {
        private const string RecordName = nameof(SendResponseRecord);
        private const string ResponseParameterKey = "ResponseParameter";

        public SendResponseRecord()
            : base(SendResponseRecord.RecordName)
        { }

        public SendResponseRecord(object responseParameter)
            : this()
            => this.ResponseParameter = responseParameter;

        public object ResponseParameter
        {
            get => this.Data.TryGetValue(ResponseParameterKey, out var responseParameter) ? responseParameter : null;
            private set => this.Data[ResponseParameterKey] = value;
        }
    }
}
