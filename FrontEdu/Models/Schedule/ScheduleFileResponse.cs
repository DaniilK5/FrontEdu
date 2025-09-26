using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrontEdu.Models.Schedule
{
    public class ScheduleFileResponse
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public DateTime UploadDate { get; set; }
        public string UploadedBy { get; set; }
        public string ContentType { get; set; }
    }
}
