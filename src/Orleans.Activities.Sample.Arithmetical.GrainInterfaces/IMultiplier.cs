using System.Threading.Tasks;
using Orleans;

namespace Orleans.Activities.Sample.Arithmetical.GrainInterfaces
{
    // The IMultiplier will use a callback call to deliver the result.
    // This is a demonstration for a long running workflow. A real grain can use streams or callback other grains to deliver the result.
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
