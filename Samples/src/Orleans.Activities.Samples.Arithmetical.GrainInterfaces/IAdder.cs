using System.Threading.Tasks;
using Orleans;

namespace Orleans.Activities.Samples.Arithmetical.GrainInterfaces
{
    public interface IAdder : IGrainWithGuidKey
    {
        Task<int> AddAsync(int arg1, int arg2);
    }
}
