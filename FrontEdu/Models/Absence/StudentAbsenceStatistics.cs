using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrontEdu.Models.Absence
{
    public class StudentAbsenceStatistics
    {
        public int StudentId { get; set; }
        public string Student { get; set; }
        public int TotalHours { get; set; }
        public int ExcusedHours { get; set; }
        public int UnexcusedHours { get; set; }
        public List<AbsenceDate> AbsenceDates { get; set; }
    }
}
