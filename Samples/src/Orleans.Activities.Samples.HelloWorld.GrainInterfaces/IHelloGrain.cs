using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Orleans;

namespace Orleans.Activities.Samples.HelloWorld.GrainInterfaces
{
    public interface IHelloGrain : IGrainWithGuidKey
    {
        Task<string> SayHelloAsync(string greeting);
        Task<string> SayByeAsync();
    }
}
