using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrontEdu.Models.Absence
{
    public class GroupAbsenceStatistics
    {
        public string GroupName { get; set; }
        public int TotalStudents { get; set; }
        public int TotalAbsenceHours { get; set; }
        public double AverageAbsenceHours { get; set; }
        public int ExcusedHours { get; set; }
        public int UnexcusedHours { get; set; }
        public List<StudentAbsenceStatistics> StudentStatistics { get; set; }
    }
}
