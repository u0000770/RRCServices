using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RRCServices.Calculator
{
    public sealed class RunnerPredictionDto
    {
        public int RunnerId { get; set; }
        public string RunnerName { get; set; } = "";
        public int RaceEventId { get; set; }
        public int EventId { get; set; }
        public DateTime RaceDate { get; set; }

        public double RaceDistanceMeters { get; set; }

        public int RecentRaceCountUsed { get; set; }
        public int? PredictedSeconds { get; set; }

        public int? ExistingTargetSeconds { get; set; }
        public bool WillOverwrite => ExistingTargetSeconds.HasValue && ExistingTargetSeconds.Value > 0;
    }

}
