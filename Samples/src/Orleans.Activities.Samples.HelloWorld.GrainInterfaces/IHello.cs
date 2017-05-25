using System.Threading.Tasks;
using Orleans;

namespace Orleans.Activities.Samples.HelloWorld.GrainInterfaces
{
    public interface IHello : IGrainWithGuidKey
    {
        Task<string> SayHelloAsync(string greeting);
        Task<string> SayByeAsync();
    }
}
