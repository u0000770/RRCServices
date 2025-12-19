using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RRCServices.Clock
{
    public interface IClock
    {
        DateTime Now { get; }
    }


    public class SystemClock : IClock
    {
        public DateTime Now => DateTime.Now;
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
