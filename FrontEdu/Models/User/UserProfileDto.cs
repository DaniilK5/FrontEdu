using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrontEdu.Models.User
{
    public class UserProfileDto
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public string StudentGroup { get; set; }
        public string Address { get; set; }
        public string PhoneNumber { get; set; }
        public string SocialStatus { get; set; }
        public string StudentId { get; set; }
        public int? StudentGroupId { get; set; }
        public RelatedUsersInfo RelatedUsers { get; set; }
        public GroupInfo GroupInfo { get; set; }
    }

    public class RelatedUsersInfo
    {
        public List<UserProfileDto> Parents { get; set; } = new();
        public List<UserProfileDto> Children { get; set; } = new();
    }

    public class GroupInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int CuratorId { get; set; }
        public string CuratorName { get; set; }
    }
}
