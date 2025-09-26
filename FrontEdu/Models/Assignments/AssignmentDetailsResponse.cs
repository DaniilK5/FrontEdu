using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrontEdu.Models.Assignments
{
    public class AssignmentDetailsResponse
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime Deadline { get; set; }
        public CourseInfo Course { get; set; }
        public AssignmentInstructor Instructor { get; set; }
        public bool HasAttachment { get; set; }
        public string AttachmentName { get; set; }
        public SubmissionInfo StudentSubmission { get; set; }
    }

    public class CourseInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class SubmissionInfo
    {
        public int Id { get; set; }
        public string Content { get; set; }
        public DateTime SubmittedAt { get; set; }
        public StudentInfo Student { get; set; }
        public bool HasAttachment { get; set; }
        public string AttachmentName { get; set; }
        public GradeInfo Grade { get; set; }
    }

    public class StudentInfo
    {
        public int Id { get; set; }
        public string FullName { get; set; }
    }

    public class GradeInfo
    {
        public int Value { get; set; }
        public string Comment { get; set; }
        public DateTime GradedAt { get; set; }
    }
}
