using FrontEdu.Models.Assignments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrontEdu.Models.Course
{
    public class UpdateCourseResponse
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public SubjectCourseInfo Subject { get; set; }
        public List<TeacherInfo> Teachers { get; set; }
        public List<StudentInfo> Students { get; set; }
        public int StudentsCount { get; set; }
        public int TeachersCount { get; set; }
        public CourseChanges Changes { get; set; }
    }
    public class CourseChanges
    {
        public int TeachersAdded { get; set; }
        public int TeachersRemoved { get; set; }
        public int StudentsAdded { get; set; }
        public int StudentsRemoved { get; set; }
    }
}
