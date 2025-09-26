using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrontEdu.Models.Admin
{
    public class AddStudentsResponse
    {
        public List<StudentInfo> AddedStudents { get; set; }
    }

    public class StudentInfo
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string StudentId { get; set; }
    }

    public class SetCuratorResponse
    {
        public int GroupId { get; set; }
        public string GroupName { get; set; }
        public CuratorInfo Curator { get; set; }
    }

    public class RemoveCuratorResponse
    {
        public string Message { get; set; }
        public int GroupId { get; set; }
        public string GroupName { get; set; }
        public CuratorInfo RemovedCurator { get; set; }
    }

    public class CuratorInfo
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
    }

    public class SetCuratorRequest
    {
        public int TeacherId { get; set; }
    }
}
