using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrontEdu.Models.Absence
{
    public class AbsenceDate
    {
        public DateTime Date { get; set; }
        public int Hours { get; set; }
        public bool IsExcused { get; set; }
        public string Reason { get; set; }
    }
}
