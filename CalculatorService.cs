using Calculator;
using Microsoft.EntityFrameworkCore;
using RRCDataModel.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RRCServices
{
    public class CalculatorService
    {
        private readonly IDbContextFactory<RRCContext> _factory;

        public CalculatorService(IDbContextFactory<RRCContext> factory)
        {
            _factory = factory;
        }


        /// <summary>
        /// Predicts a runner's time for a new race distance by
        /// averaging predictions derived from their most recent races.
        /// </summary>
        /// <param name="recentRaces">
        /// A list of recent completed races (distance in metres, time in seconds)
        /// </param>
        /// <param name="newDistanceMeters">
        /// The distance of the upcoming race in metres
        /// </param>
        /// <returns>
        /// Average predicted time in seconds, or null if no races supplied
        /// </returns>
        public static double? PredictFromRecentRaces(
            IReadOnlyCollection<RecentRaceDto> recentRaces,
            double newDistanceMeters)
        {
            // 1️⃣ Defensive guard — no data means no prediction
            if (recentRaces == null || recentRaces.Count == 0)
                return null;

            double totalPredictedSeconds = 0;
            int predictionCount = 0;

            // 2️⃣ Loop through each recent race
            foreach (var race in recentRaces)
            {
                // Extra defensive checks (should already be clean, but safe)
                if (race.Distance <= 0 || race.Actual <= 0)
                    continue;

                // 3️⃣ Predict time for THIS race → new distance
                var predictedSeconds = CalculatePredicion(
                    nextDistance: newDistanceMeters,
                    oldDistance: race.Distance,
                    oldTime: race.Actual
                );

                totalPredictedSeconds += predictedSeconds;
                predictionCount++;
            }

            // 4️⃣ Avoid divide-by-zero if all races were skipped
            if (predictionCount == 0)
                return null;

            // 5️⃣ Return average predicted time (seconds)
            return totalPredictedSeconds / predictionCount;
        }


        // ---------------------------------------------
        // Prediction logic 
        // ---------------------------------------------
        public static double CalculatePredicion(
            double nextDistance,
            double oldDistance,
            int oldTime)
        {
            return (
                RaceCalc.calcPredictedTime(oldDistance, nextDistance, oldTime) +
                RaceCalc.cameron(oldDistance, nextDistance, oldTime)
            ) / 2;
        }

        // ---------------------------------------------
        // Get last N completed races (excluding 1 mile)
        // ---------------------------------------------
        public async Task<List<RecentRaceDto>> GetLastRacesAsync(
            int runnerId,
            int maxRaces = 3,
            CancellationToken ct = default)
        {
            // ✅ Correct usage of DbContextFactory
            await using var db = await _factory.CreateDbContextAsync(ct);

            return await db.EventRunnerTimes
                .AsNoTracking()

                // 1️⃣ Filter to valid, completed races
                .Where(t =>
                    t.RunnerId == runnerId &&
                    t.Active != false &&
                    t.Actual.HasValue &&
                    t.Date.HasValue &&
                    t.Event.Active != false &&
                    t.Event.DistanceCode != "1m"   // exclude mile races
                )

                // 2️⃣ Most recent races first
                .OrderByDescending(t => t.Date)

                // 3️⃣ Pull only the required data
                .Select(t => new
                {
                    t.RunnerId,
                    ActualSeconds = t.Actual!.Value,

                    // Distance is stored in METRES in the distance table
                    DistanceMeters = db.distance
                        .Where(d => d.Code == t.Event.DistanceCode)
                        .Select(d => d.Distance1)
                        .FirstOrDefault()
                })

                // 4️⃣ Ensure distance lookup succeeded
                .Where(x => x.DistanceMeters > 0)

                // 5️⃣ Take the most recent N races
                .Take(maxRaces)

                // 6️⃣ Map to DTO
                .Select(x => new RecentRaceDto
                {
                    RunnerId = x.RunnerId,
                    Actual = x.ActualSeconds,
                    Distance = x.DistanceMeters   // metres
                })

                .ToListAsync(ct);
        }
    }


    public class RecentRaceDto
    {
        public int RunnerId { get; set; }
        public int Actual { get; set; }      // seconds
        public double Distance { get; set; } // meters
    }
}
