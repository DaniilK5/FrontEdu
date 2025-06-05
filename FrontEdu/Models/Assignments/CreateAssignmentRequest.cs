using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrontEdu.Models.Assignments
{
    public class CreateAssignmentRequest
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime Deadline { get; set; }
        public int CourseId { get; set; }
    }

    public class CreateAssignmentResponse
    {
        public int AssignmentId { get; set; }
    }
}
