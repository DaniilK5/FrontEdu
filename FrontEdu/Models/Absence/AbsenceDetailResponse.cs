using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrontEdu.Models.Absence
{
    public class AbsenceDetailResponse
    {
        public StudentInfo StudentInfo { get; set; }
        public int TotalHours { get; set; }
        public int ExcusedHours { get; set; }
        public int UnexcusedHours { get; set; }
        public List<AbsenceRecord> Absences { get; set; }
    }
}
