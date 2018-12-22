using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CalDav.Models
{
    public class CalDavServer
    {
        [Key]
        public int Id { get; set; }
        public string Host { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string UserDir { get; set; }
        public string CalendarHomeSet { get; set; }
        [InverseProperty("Server")]
        public virtual List<CalDavCalendar> Calendars { get; set; }

        public bool Valid => UserDir != null && CalendarHomeSet != null;
    }
}
