using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrontEdu.Models.Absence
{
    public class ParentChildAbsenceDetails
    {
        public StudentBasicInfo Student { get; set; }
        public int TotalHours { get; set; }
        public int ExcusedHours { get; set; }
        public int UnexcusedHours { get; set; }
        public List<AbsenceRecordInfo> Absences { get; set; }
    }

    public class StudentBasicInfo
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string StudentGroup { get; set; }
        public string StudentId { get; set; }
    }

    public class AbsenceRecordInfo
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public int Hours { get; set; }
        public string Reason { get; set; }
        public bool IsExcused { get; set; }
        public string Comment { get; set; }
        public InstructorInfo Instructor { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
