using FrontEdu.Models.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrontEdu.Models.GroupChat
{
    public class GroupChatResponse
    {
        public int TotalCount { get; set; }
        public List<GroupChatListItem> Chats { get; set; }
    }

    public class GroupChatListItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DateTime CreatedAt { get; set; }
        public int MembersCount { get; set; }
        public bool IsAdmin { get; set; }
        public LastMessage LastMessage { get; set; }
        public List<GroupChatMember> Members { get; set; }
        public int UnreadCount { get; set; }
    }

    public class LastMessage
    {
        public int Id { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }
        public ChatUserDto Sender { get; set; }
        public bool HasAttachment { get; set; }
        public string AttachmentName { get; set; }
    }

    public class GroupChatMember
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public bool IsAdmin { get; set; }
    }
}
