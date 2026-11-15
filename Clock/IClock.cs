namespace RRCServices.Clock
{
    // =========================================================================
    // IClock — the single source of "now" for services and pages
    // =========================================================================
    // CHANGE (season work, batch 1):
    //   + Today (DateOnly). It's a default interface member worked out from
    //     Now, so every existing IClock implementation (FixedClock and the
    //     copies in RRCApp / PublicApp / rrcapi) keeps compiling unchanged.
    //   + SystemClock now returns UK local time (Europe/London) whatever the
    //     server's time zone. Azure App Service runs in UTC, so DateTime.Now
    //     there is an hour behind during BST.
    //
    // SEASON WORK (batch 5): the host copies of SystemClock (RRCApp/Class.cs,
    // PublicApp/clock.cs, rrcapi/Class.cs and the one in rrcapi/Program.cs)
    // have been deleted. RRCApp, PublicApp and rrcapi all use this one now.
    // =========================================================================
    public interface IClock
    {
        DateTime Now { get; }

        /// <summary>Today's date in club (UK) time.</summary>
        DateOnly Today => DateOnly.FromDateTime(Now);
    }

    public class SystemClock : IClock
    {
        private static readonly TimeZoneInfo UkTime = LoadUkTimeZone();

        public DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, UkTime);

        private static TimeZoneInfo LoadUkTimeZone()
        {
            try
            {
                // IANA id: works on Linux, and on Windows with .NET 6+ / ICU
                return TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
            }
            catch (TimeZoneNotFoundException)
            {
                // Windows id fallback
                return TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");
            }
        }
    }

    public class FixedClock : IClock
    {
        public DateTime Now { get; }

        public FixedClock(DateTime fixedNow)
        {
            Now = fixedNow;
        }
    }
}
