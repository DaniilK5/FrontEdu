using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrontEdu.Models.Chat
{
    public class SendMessageDto
    {
        public int? ReceiverId { get; set; }
        public int? GroupChatId { get; set; }
        public string Content { get; set; }
        public IFormFile Attachment { get; set; }
    }
}
