using System.Threading.Tasks;
using Orleans;

namespace Orleans.Activities.Sample.Arithmetical.GrainInterfaces
{
    public interface IMultiplier : IGrainWithGuidKey
    {
        Task MultiplyAsync(int arg1, int arg2);

        Task Subscribe(IMultiplierResultReceiver observer);
        Task Unsubscribe(IMultiplierResultReceiver observer);
    }

    public interface IMultiplierResultReceiver : IGrainObserver
    {
        void ReceiveResult(int result);
    }
}
