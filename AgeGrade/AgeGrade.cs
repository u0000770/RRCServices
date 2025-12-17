using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RRCServices.AgeGrade
{
    public class AgeGrade
    {

        public static int GetWavScore(int time, bool gender, DateTime? dob, string RaceCode, DateTime? RaceDate)
        {
            WAVAGrade model = new WAVAGrade();
            return (int)model.GetGrade((DateTime)dob, gender, RaceCode, time, (DateTime)RaceDate);

        }
    }
}
