using RRCServices.League.DTO;

namespace RRCServices.League.Trophy
{
    // =========================================================================
    // TrophyCandidate
    // =========================================================================
    // One runner's complete scoring picture, exactly as the assignment resolver
    // needs it. Built from the same ScoreRunner output already used to score both
    // trophies, plus a JR-eligibility flag (did the runner race anything > 10k?).
    //
    // It is deliberately a plain data carrier with no behaviour beyond two derived
    // eligibility flags, so it is trivial to construct in unit tests without a DB.
    // =========================================================================
    internal sealed class TrophyCandidate
    {
        public required int RunnerId { get; init; }
        public required string RunnerName { get; init; }

        public required int JrPoints { get; init; }
        public required int JrTimeDiff { get; init; }

        public required int DbPoints { get; init; }
        public required int DbTimeDiff { get; init; }

        // True when the runner has run at least one race longer than 10k.
        // A runner who has not is NOT permitted in the JR trophy (rule D).
        public required bool JrEligible { get; init; }

        // Derived eligibility:
        //   CanJr — eligible for JR (ran a long race) AND actually scored in JR
        //   CanDb — scored in DB (has at least one qualifying DB-distance race)
        public bool CanJr => JrEligible && JrPoints > 0;
        public bool CanDb => DbPoints > 0;
    }


    // =========================================================================
    // TrophyAssignmentResolver
    // =========================================================================
    // Single source of truth for "which trophy does each runner end up in?".
    //
    // END-OF-SEASON RULES (agreed):
    //   D. Eligibility — a runner may only be placed in JR if they have run a race
    //      longer than 10k. They may only be placed in DB if they have a qualifying
    //      short-distance race.
    //   E. Ranking within a trophy — points desc, then trophy-time desc, then name.
    //   F. Assignment — each runner is placed in the trophy where their FINAL,
    //      settled position is best (lowest number). Equal position -> JR.
    //
    // WHY "SETTLED" (and not a single comparison):
    //   A runner's position depends on who else is in that table, which itself
    //   depends on everyone's assignment. Moving a strong DB runner into DB pushes
    //   weaker DB runners down a place, which can change where THEY belong. So a
    //   single pass is not enough — we iterate best-responses until no runner would
    //   move (a stable assignment).
    //
    //   This replaces the previous logic, which compared raw points. That was wrong
    //   for two reasons: it ignored JR eligibility entirely, and it was biased
    //   toward JR because JR scores 8 races to DB's 6, so a runner's JR points are
    //   almost always >= their DB points regardless of where they actually rank.
    // =========================================================================
    internal static class TrophyAssignmentResolver
    {
        // Maps each runner to the single trophy they end up in. Runners who scored
        // in neither trophy are omitted from the result.
        public static IReadOnlyDictionary<int, TrophyType> Resolve(
            IReadOnlyCollection<TrophyCandidate> candidates)
        {
            var assignment = new Dictionary<int, TrophyType>(candidates.Count);

            // ----- Seed -----
            // Forced choices first (runners eligible for only one table). The rest
            // start in JR. The final result is independent of this seed — the settle
            // below converges to the same stable assignment from any starting point.
            foreach (TrophyCandidate c in candidates)
            {
                if (!c.CanJr && !c.CanDb) continue;                       // scored nowhere
                if (!c.CanJr) { assignment[c.RunnerId] = TrophyType.DB; continue; }
                if (!c.CanDb) { assignment[c.RunnerId] = TrophyType.JR; continue; }
                assignment[c.RunnerId] = TrophyType.JR;                  // flexible — seed value
            }

            // Only runners eligible for BOTH trophies can move between tables.
            List<TrophyCandidate> flexible = candidates
                .Where(c => c.CanJr && c.CanDb)
                .OrderBy(c => c.RunnerId)
                .ToList();

            // ----- Settle -----
            // Iterate best-responses until nobody would improve their position by
            // switching. Positions are read live from `assignment`, so a change made
            // earlier in a pass is visible later in the same pass (sequential update),
            // which converges quickly and avoids the oscillation a fully simultaneous
            // update can cause. The iteration cap is a safety net only — for real
            // data this settles in a handful of passes.
            int maxIterations = (candidates.Count * 4) + 50;

            for (int iter = 0; iter < maxIterations; iter++)
            {
                bool changed = false;

                foreach (TrophyCandidate x in flexible)
                {
                    int jrPos = PositionIn(candidates, assignment, x, TrophyType.JR);
                    int dbPos = PositionIn(candidates, assignment, x, TrophyType.DB);

                    // Best (lowest) position wins; equal position -> JR (rule F).
                    TrophyType want = dbPos < jrPos ? TrophyType.DB : TrophyType.JR;

                    if (assignment[x.RunnerId] != want)
                    {
                        assignment[x.RunnerId] = want;
                        changed = true;
                    }
                }

                if (!changed) break;
            }

            return assignment;
        }


        // The position the runner WOULD hold in the given trophy under the current
        // assignment: 1 + (number of runners currently in that trophy who outrank
        // them). Works whether or not the runner is themselves assigned there, which
        // is what lets us compare "where would I sit in the other table?".
        private static int PositionIn(
            IReadOnlyCollection<TrophyCandidate> candidates,
            IReadOnlyDictionary<int, TrophyType> assignment,
            TrophyCandidate x,
            TrophyType trophy)
        {
            int ahead = 0;
            foreach (TrophyCandidate y in candidates)
            {
                if (y.RunnerId == x.RunnerId) continue;
                if (!assignment.TryGetValue(y.RunnerId, out TrophyType t) || t != trophy) continue;
                if (Outranks(y, x, trophy)) ahead++;
            }
            return ahead + 1;
        }


        // Ranking order within a trophy (rule E): points desc, then trophy-time desc,
        // then name asc (ordinal, purely so ties resolve deterministically).
        private static bool Outranks(TrophyCandidate y, TrophyCandidate x, TrophyType trophy)
        {
            (int yPoints, int yTime) = trophy == TrophyType.JR
                ? (y.JrPoints, y.JrTimeDiff)
                : (y.DbPoints, y.DbTimeDiff);

            (int xPoints, int xTime) = trophy == TrophyType.JR
                ? (x.JrPoints, x.JrTimeDiff)
                : (x.DbPoints, x.DbTimeDiff);

            if (yPoints != xPoints) return yPoints > xPoints;
            if (yTime != xTime) return yTime > xTime;
            return string.CompareOrdinal(y.RunnerName, x.RunnerName) < 0;
        }
    }
}
