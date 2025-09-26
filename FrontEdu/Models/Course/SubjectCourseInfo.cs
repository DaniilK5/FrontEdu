using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrontEdu.Models.Course
{
    public class SubjectCourseInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Code { get; set; }  // Добавлено
        public DateTime CreatedAt { get; set; }
        public List<SubjectTeacherInfo> Teachers { get; set; }
        public int StudentsCount { get; set; }
        public int CoursesCount { get; set; }  // Добавлено
    }
}
