using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrontEdu.Models.Course
{
    public class SubjectCoursesResponse
    {
        public string Name { get; set; }
        public string Code { get; set; }
        public List<SubjectCourseInfo> Courses { get; set; }
    }
}
