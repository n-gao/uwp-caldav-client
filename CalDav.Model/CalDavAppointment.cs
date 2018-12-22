using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CalDav.Models
{
    public class CalDavAppointment
    {
        public int CalendarId { get; set; }
        [ForeignKey("CalendarId")]
        public virtual CalDavCalendar Calendar { get; set; }
        public string Href { get; set; }
        public string LocalId { get; set; }
        public string Etag { get; set; }
    }
}
