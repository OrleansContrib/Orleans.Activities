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

            this.TrackingProfile = new TrackingProfile();
            this.TrackingProfile.Queries.Add(new WorkflowInstanceQuery()
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
            this.TrackingProfile.Queries.Add(new BookmarkResumptionQuery()
            {
                Name = "*",
            });
            this.TrackingProfile.Queries.Add(new CancelRequestedQuery()
            {
                ActivityName = "*",
            });
            this.TrackingProfile.Queries.Add(new CustomTrackingQuery()
            {
                ActivityName = "*",
                Name = "*"
            });
            this.TrackingProfile.Queries.Add(new FaultPropagationQuery()
            {
                FaultSourceActivityName = "*",
            });
        }

        protected override Task TrackAsync(TrackingRecord record, TimeSpan timeout)
        {
            this.logger.Info(record.ToString());
            return Task.CompletedTask;
        }
    }
}
