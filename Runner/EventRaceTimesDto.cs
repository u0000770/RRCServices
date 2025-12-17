using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RRCServices.Runner
{
    public sealed class EventRaceTimesDto
    {
        public int EventRunnerTimeId { get; init; } // EventRunnerTimes.EFKey
        public int EventId { get; init; }

        public string RaceDistance { get; init; } = "";  // e.g. from Events
        public string RaceTitle { get; init; } = "";     // e.g. from Events

        public int TargetTime { get; init; }             // seconds
        public string RaceTargetTime { get; init; } = "No Result";
        public string RaceActualTime { get; init; } = "No Result";

        public DateTime? RaceDate { get; init; }
        public string TimeDifference { get; init; } = "";

        public decimal AgeGrade { get; init; }           // hook into your AgeGrade logic
    }

}
