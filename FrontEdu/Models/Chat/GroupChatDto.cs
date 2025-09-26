using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrontEdu.Models.Chat
{
    public class GroupChatDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<ChatUserDto> Members { get; set; }
        public DateTime CreatedAt { get; set; }
        public ChatUserDto Creator { get; set; }
    }

    public class CreateGroupChatDto
    {
        public string Name { get; set; }
        public List<int> MemberIds { get; set; } = new();
    }
}
