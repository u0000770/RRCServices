using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RRCServices.AgeGrade
{
    public class AgeGrade
    {
        // AgeGrade.GetWavScore(t.ActualSeconds!.Value,_runner.Gender, _runner.Dob, DistanceService.GetCodeByMetersAsync(race.distance), t.RaceDate).ToString("0.00")
        public static int GetWavScore(int time, bool gender, DateTime? dob, string RaceCode, DateTime? RaceDate)
        {
            WAVAGrade model = new WAVAGrade();
            return (int)model.GetGrade((DateTime)dob, gender, RaceCode, time, (DateTime)RaceDate);

        }
    }
}
