using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace FrontEdu.Models.Absence
{
    public class AbsenceRecord
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public int Hours { get; set; }
        public string Reason { get; set; }
        public bool IsExcused { get; set; }
        public string Comment { get; set; }
        public StudentInfo Student { get; set; }
        public InstructorInfo Instructor { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
