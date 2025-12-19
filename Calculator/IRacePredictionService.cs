using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RRCServices.Calculator
{
    Task<List<RunnerPredictionDto>> PredictTargetsForRaceEventAsync(
     int raceEventId,
     int lastN = 3,
     CancellationToken ct = default);

}
