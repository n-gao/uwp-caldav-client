using System;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;
using System.Diagnostics;

using Windows.ApplicationModel.Appointments;

using Microsoft.EntityFrameworkCore;

using CalDav.CalDav;
using CalDav.Models;

namespace CalDav.Views
{
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        public MainPage()
        {
            InitializeComponent();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Set<T>(ref T storage, T value, [CallerMemberName]string propertyName = null)
        {
            if (Equals(storage, value))
            {
                return;
            }

            storage = value;
            OnPropertyChanged(propertyName);
        }

        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private void Button_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Debug.WriteLine("Hello World!");
            CalDavTest();
        }

        private async Task<CalDavServer> GetServer(string host, string user, string password)
        {
            using (var db = new CalDavContext())
            {
                try
                {
                    return await db.Servers.FirstAsync(s => s.Host == host && s.Username == user);
                } catch(Exception) { }
                var result = new CalDavServer
                {
                    Host = host,
                    Username = user,
                    Password = password
                };
                await db.Servers.AddAsync(result);
                await db.SaveChangesAsync();
                return result;
            }
        }

        private CalDavServer server;
        private CalDavClient client;

        private async void CalDavTest()
        {
            if (server == null)
                server = await GetServer("http://localhost:5232", "user", "12345");
            if (client == null)
            {
                 client = new CalDavClient(server);
                await client.Prepare();
            }
            await client.UploadChanges();
        }

        private async void DoStuff()
        {
            var store = await AppointmentManager.RequestStoreAsync(AppointmentStoreAccessType.AppCalendarsReadWrite);
            var calendars = await store.FindAppointmentCalendarsAsync();
            var calendar = calendars.FirstOrDefault();
            if (calendar == null)
                calendar = await store.CreateAppointmentCalendarAsync("MyCalendar");
            var appointment = new Appointment
            {
                AllDay = true,
                StartTime = DateTime.Now.AddHours(1),
                Subject = "Test"
            };
            await calendar.SaveAppointmentAsync(appointment);

        }
    }
}
