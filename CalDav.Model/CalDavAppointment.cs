using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CalDav.Models
{
    public class CalDavAppointment
    {
        public string CalHref { get; set; }
        [ForeignKey("CalHref")]
        public virtual CalDavCalendar Calendar { get; set; }
        [Key]
        public string Href { get; set; }
        public string LocalId { get; set; }
        public string Etag { get; set; }
    }
}
