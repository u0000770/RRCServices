namespace RRCServices.Calculator
{
    // =========================================================================
    // PredictionCapRule — the 7% "bad day" cap on prediction inputs
    // =========================================================================
    // When the predictor looks back at a runner's recent races, any race that
    // was more than 7% slower than its target is read AS IF the runner had
    // finished exactly 7% slower. One off day no longer drags the next target
    // too far in the wrong direction.
    //
    // RULES (agreed 19 Nov 2026, tested in PredictionCapRuleTests):
    //   R1  cap = target x 1.07, rounded to the nearest whole second.
    //   R2  actual slower than the cap  -> use the cap.
    //       actual at or inside the cap -> use the actual.
    //   R3  no target (null or 0)       -> use the actual.
    //   R4  faster than target          -> never changed (no floor).
    //
    // WHAT THIS DOES NOT CHANGE (R7):
    //   This class is used ONLY by the prediction calculation. Recorded times,
    //   trophy points, the tie-break, league tables, age grade and results
    //   pages all keep using the real actual time. Nothing is saved.
    //
    // WHY WHOLE-NUMBER MATHS:
    //   1950 x 1.07 = 2086.5. As a double this can come out as 2086.4999...,
    //   and Math.Round uses banker's rounding by default, so the answer could
    //   be 2086 instead of 2087. Working in whole numbers avoids both:
    //   (target x 107 + 50) / 100 always rounds a half up.
    // =========================================================================
    public static class PredictionCapRule
    {
        /// <summary>How much slower than target a race may be before it is capped.</summary>
        public const int CapPercent = 7;

        /// <summary>
        /// The slowest time the predictor will read for a race with this target:
        /// target + 7%, rounded to the nearest second.
        /// </summary>
        public static int CapSeconds(int targetSeconds)
        {
            return (targetSeconds * (100 + CapPercent) + 50) / 100;
        }

        /// <summary>
        /// True when the race had a target and the actual time was slower than the cap.
        /// </summary>
        public static bool IsCapped(int actualSeconds, int? targetSeconds)
        {
            if (!targetSeconds.HasValue || targetSeconds.Value <= 0)
                return false;   // R3: nothing to cap against

            return actualSeconds > CapSeconds(targetSeconds.Value);
        }

        /// <summary>
        /// The time the predictor should read for this race:
        /// the cap if the race was capped, otherwise the real actual time.
        /// </summary>
        public static int TimeForPrediction(int actualSeconds, int? targetSeconds)
        {
            if (IsCapped(actualSeconds, targetSeconds))
                return CapSeconds(targetSeconds!.Value);

            return actualSeconds;
        }
    }
}
