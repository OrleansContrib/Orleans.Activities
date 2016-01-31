using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Runtime.Serialization;

namespace Orleans.Activities.Test.Activities
{
    [Serializable]
    public class TestException : Exception
    {
        public TestException() : base() {}
        public TestException(string message) : base(message) { }
        protected TestException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
