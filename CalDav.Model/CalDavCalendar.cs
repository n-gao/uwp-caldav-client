using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CalDav.Models
{
    public class CalDavCalendar
    {
        [Key]
        public string Href { get; set; }
        public string Ctag { get; set; }
        public string SyncToken { get; set; }
        public string DisplayName { get; set; }

        public string LocalId { get; set; }

        public int ServerId { get; set; }
        [ForeignKey("ServerId")]
        public virtual CalDavServer Server { get; set; }
        public bool Initialized { get; set; }

        [InverseProperty("Calendar")]
        public virtual List<CalDavAppointment> Appointments { get; set; }

        public CalDavCalendar() { }
    }

}
