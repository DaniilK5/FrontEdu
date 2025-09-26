using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrontEdu.Models.Assignments
{
    public class AssignmentResponse
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime Deadline { get; set; }
        public AssignmentInstructor Instructor { get; set; }
    }

    public class AssignmentInstructor
    {
        public int Id { get; set; }
        public string FullName { get; set; }
    }
}
