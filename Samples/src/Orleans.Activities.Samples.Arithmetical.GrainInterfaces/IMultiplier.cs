using System.Threading.Tasks;
using Orleans;

namespace Orleans.Activities.Samples.Arithmetical.GrainInterfaces
{
    // The IMultiplier will use a callback call to deliver the result.
    // This is a demonstration for a long running workflow. A real grain can use streams or callback other grains to deliver the result.
    public interface IMultiplier : IGrainWithGuidKey
    {
        Task MultiplyAsync(int arg1, int arg2);

        Task SubscribeAsync(IMultiplierResultReceiver observer);
        Task UnsubscribeAsync(IMultiplierResultReceiver observer);
    }

    public interface IMultiplierResultReceiver : IGrainObserver
    {
        void ReceiveResult(int result);
    }
}
