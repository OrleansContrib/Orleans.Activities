using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Orleans;

using System.Activities.Tracking;
using Orleans.Runtime;
using Orleans.Activities.Tracking;

namespace Orleans.Activities.Samples.Arithmetical.Grains
{
    public sealed class GrainTrackingParticipant : Orleans.Activities.Tracking.TrackingParticipant
    {
        private Logger logger;

        public GrainTrackingParticipant(Logger logger)
        {
            this.logger = logger;

            TrackingProfile = new TrackingProfile();
            TrackingProfile.Queries.Add(new WorkflowInstanceQuery()
            {
                States = { "*" },
            });
            //TrackingProfile.Queries.Add(new ActivityScheduledQuery()
            //{
            //    ActivityName = "*",
            //});
            //TrackingProfile.Queries.Add(new ActivityStateQuery()
            //{
            //    States = { "*" }
            //});
            TrackingProfile.Queries.Add(new BookmarkResumptionQuery()
            {
                Name = "*",
            });
            TrackingProfile.Queries.Add(new CancelRequestedQuery()
            {
                ActivityName = "*",
            });
            TrackingProfile.Queries.Add(new CustomTrackingQuery()
            {
                ActivityName = "*",
                Name = "*"
            });
            TrackingProfile.Queries.Add(new FaultPropagationQuery()
            {
                FaultSourceActivityName = "*",
            });
        }

        protected override Task TrackAsync(TrackingRecord record, TimeSpan timeout)
        {
            logger.Info(record.ToString());
            return Task.CompletedTask;
        }
    }
}
