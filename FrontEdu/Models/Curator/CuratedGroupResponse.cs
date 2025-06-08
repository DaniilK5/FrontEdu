using FrontEdu.Models.User;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrontEdu.Models.Curator
{
    public class CuratedGroupResponse
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int StudentsCount { get; set; }
    }

    public class GroupStatistics
    {
        public int StudentsCount { get; set; }
        public double AverageGroupGrade { get; set; }
        public GradeDistribution GradeDistribution { get; set; }
        public AttendanceStatistics Attendance { get; set; }
        public Period Period { get; set; }
    }

    public class Period
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class GradeDistribution
    {
        public int Excellent { get; set; }
        public int Good { get; set; }
        public int Satisfactory { get; set; }
        public int Poor { get; set; }
    }

    public class AttendanceStatistics
    {
        public int TotalAbsenceHours { get; set; }
        public int ExcusedHours { get; set; }
        public int UnexcusedHours { get; set; }
        public double AverageAbsenceHoursPerStudent { get; set; }
    }

    public class StudentDetails
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string StudentId { get; set; }
        public List<GradeInfo> Grades { get; set; }
        public List<AbsenceInfo> Absences { get; set; }
        public PerformanceInfo Performance { get; set; }
        public StudentAttendance Attendance { get; set; }
    }

    public class GradeInfo
    {
        public int Value { get; set; }
        public DateTime GradedAt { get; set; }
        public string Subject { get; set; }
        public string Course { get; set; }
        public string Assignment { get; set; }
    }

    public class AbsenceInfo
    {
        public DateTime Date { get; set; }
        public int Hours { get; set; }
        public bool IsExcused { get; set; }
        public string Reason { get; set; }
    }

    public class PerformanceInfo
    {
        public double AverageGrade { get; set; }
        public List<SubjectPerformance> SubjectsPerformance { get; set; }
    }

    public class SubjectPerformance
    {
        public string Subject { get; set; }
        public double AverageGrade { get; set; }
        public int GradesCount { get; set; }
        public int LatestGrade { get; set; }
    }

    public class StudentAttendance
    {
        public int TotalAbsences { get; set; }
        public int ExcusedAbsences { get; set; }
        public int UnexcusedAbsences { get; set; }
    }

    public class GroupCuratorResponse
    {
        public GroupInfo GroupInfo { get; set; }
        public GroupStatistics Statistics { get; set; }
        public List<StudentDetails> Students { get; set; }
    }
}
