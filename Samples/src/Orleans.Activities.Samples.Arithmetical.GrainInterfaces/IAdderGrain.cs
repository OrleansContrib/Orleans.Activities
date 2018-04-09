using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Orleans;

namespace Orleans.Activities.Samples.Arithmetical.GrainInterfaces
{
    public interface IAdderGrain : IGrainWithGuidKey
    {
        Task<int> AddAsync(int arg1, int arg2);
    }
}
