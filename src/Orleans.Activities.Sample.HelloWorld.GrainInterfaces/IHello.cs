using System.Threading.Tasks;
using Orleans;

namespace Orleans.Activities.Sample.HelloWorld.GrainInterfaces
{
    public interface IHello : IGrainWithGuidKey
    {
        Task<string> SayHello(string greeting);
        Task<string> SayBye();
    }
}
