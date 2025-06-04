using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrontEdu.Models.Auth
{
    public class UserPermissionsResponse
    {
        public string Role { get; set; }
        public Permissions Permissions { get; set; }
        public Categories Categories { get; set; }
    }

    public class Permissions
    {
        public bool ManageUsers { get; set; }
        public bool ManageSettings { get; set; }
        public bool ManageCourses { get; set; }
        public bool ViewCourses { get; set; }
        public bool ManageSchedule { get; set; }
        public bool ViewSchedule { get; set; }
        public bool ManageGrades { get; set; }
        public bool ViewGrades { get; set; }
        public bool SendMessages { get; set; }
        public bool DeleteMessages { get; set; }
        public bool ManageGroupChats { get; set; }
        public bool ManageReports { get; set; }
        public bool ViewReports { get; set; }
        public bool ManageAssignments { get; set; }
        public bool ViewAssignments { get; set; }
        public bool SubmitAssignments { get; set; }
        public bool ManageStudents { get; set; }
        public bool ViewStudentDetails { get; set; }
    }

    public class Categories
    {
        public UserCategory Users { get; set; }
        public CourseCategory Courses { get; set; }
        public ScheduleCategory Schedule { get; set; }
        public GradesCategory Grades { get; set; }
        public MessagesCategory Messages { get; set; }
        public ReportsCategory Reports { get; set; }
        public AssignmentsCategory Assignments { get; set; }
        public StudentsCategory Students { get; set; }
    }

    public class UserCategory { public bool CanManage { get; set; } }
    public class CourseCategory { public bool CanManage { get; set; } public bool CanView { get; set; } }
    public class ScheduleCategory { public bool CanManage { get; set; } public bool CanView { get; set; } }
    public class GradesCategory { public bool CanManage { get; set; } public bool CanView { get; set; } }
    public class MessagesCategory { public bool CanSend { get; set; } public bool CanDelete { get; set; } public bool CanManageGroups { get; set; } }
    public class ReportsCategory { public bool CanManage { get; set; } public bool CanView { get; set; } }
    public class AssignmentsCategory { public bool CanManage { get; set; } public bool CanView { get; set; } public bool CanSubmit { get; set; } }
    public class StudentsCategory { public bool CanManage { get; set; } public bool CanViewDetails { get; set; } }

}
