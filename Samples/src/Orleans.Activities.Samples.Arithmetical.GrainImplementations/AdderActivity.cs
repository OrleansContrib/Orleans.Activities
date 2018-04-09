using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities;
using System.Activities.Statements;

namespace Orleans.Activities.Samples.Arithmetical.GrainImplementations
{
    public sealed class AdderActivity : Activity
    {
        public InArgument<int> Arg1 { get; set; } = new InArgument<int>();
        public InArgument<int> Arg2 { get; set; } = new InArgument<int>();
        public OutArgument<int> Result { get; set; } = new OutArgument<int>();

#pragma warning disable IDE0021 // Use expression body for constructors
        public AdderActivity()
        {
            this.Implementation = () => new Assign<int>
            {
                To = new OutArgument<int>(ctx => this.Result.Get(ctx)),
                Value = new InArgument<int>(ctx => this.Arg1.Get(ctx) + this.Arg2.Get(ctx)),
            };
        }
#pragma warning restore IDE0021 // Use expression body for constructors
    }
}
