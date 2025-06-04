using FrontEdu.Models.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrontEdu.Models.GroupChat
{
    public class GroupChatMessagesResponse
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<GroupChatMemberInfo> Members { get; set; }
        public List<MessageDto> Messages { get; set; }
    }
}
