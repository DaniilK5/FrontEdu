using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrontEdu.Models.Course
{
    public class CreateCourseRequest
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public int SubjectId { get; set; }
        public List<int> TeacherIds { get; set; }
        public List<int> StudentIds { get; set; }
    }

    // Models/Course/CourseResponse.cs
    public class CourseResponse
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
        public List<CourseTeacher> Teachers { get; set; }
        public int StudentsCount { get; set; }
    }

    public class CourseTeacher
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public DateTime JoinedAt { get; set; }
    }
}
