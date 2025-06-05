using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrontEdu.Models.Course
{
    public class UpdateCourseRequest
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public int SubjectId { get; set; }
        public List<int> TeacherIds { get; set; }
        public List<int> StudentIds { get; set; }
    }
}
