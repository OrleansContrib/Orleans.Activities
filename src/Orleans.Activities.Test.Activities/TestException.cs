using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orleans.Activities.Test.Activities
{
    [Serializable]
    public class TestException : Exception
    {
        public TestException() : base() {}
        public TestException(string message) : base(message) { }
    }
}
