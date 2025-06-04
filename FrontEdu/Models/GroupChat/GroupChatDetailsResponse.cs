using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrontEdu.Models.GroupChat
{
    public class GroupChatDetailsResponse
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DateTime CreatedAt { get; set; }
        public CurrentUserInfo CurrentUserInfo { get; set; }
        public List<GroupChatMemberInfo> Members { get; set; }
        public GroupChatStatistics Statistics { get; set; }
    }

    public class CurrentUserInfo
    {
        public bool IsAdmin { get; set; }
        public DateTime JoinedAt { get; set; }
    }

    public class GroupChatMemberInfo
    {
        public int UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public bool IsAdmin { get; set; }
        public DateTime JoinedAt { get; set; }
    }

    public class GroupChatStatistics
    {
        public int TotalMembers { get; set; }
        public int AdminsCount { get; set; }
        public int TotalMessages { get; set; }
        public int TotalAttachments { get; set; }
        public int UnreadMessages { get; set; }
        public int YourMessages { get; set; }
    }
}
