using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;

namespace FrontEdu.Models.Chat
{
    public class MessageDto : INotifyPropertyChanged
    {
        private string _content;
        
        public int Id { get; set; }
        public string Content 
        { 
            get => _content;
            set
            {
                if (_content != value)
                {
                    _content = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Content)));
                }
            }
        }
        public DateTime Timestamp { get; set; }
        public SenderInfo Sender { get; set; }
        public bool HasAttachment { get; set; }
        public string AttachmentName { get; set; }
        public string AttachmentType { get; set; }
        public bool IsFromCurrentUser { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class SenderInfo
    {
        public int Id { get; set; }
        public string FullName { get; set; }
    }
}
