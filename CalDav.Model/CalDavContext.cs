using Microsoft.EntityFrameworkCore;

namespace CalDav.Models
{
    public class CalDavContext : DbContext
    {
        public DbSet<CalDavAppointment> Appointments { get; set; }
        public DbSet<CalDavCalendar> Calendars { get; set; }
        public DbSet<CalDavServer> Servers { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder
                .UseLazyLoadingProxies()
                .UseSqlite("Data Source=calendars.db");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<CalDavCalendar>()
                .HasMany(c => c.Appointments)
                .WithOne(a => a.Calendar)
                .HasForeignKey(a => a.CalHref);

            modelBuilder.Entity<CalDavServer>()
                .HasMany(s => s.Calendars)
                .WithOne(c => c.Server)
                .HasForeignKey(c => c.ServerId);
        }
    }
}
