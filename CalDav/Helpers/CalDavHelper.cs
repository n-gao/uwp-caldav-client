using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using CalDav.CalDav;
using CalDav.Models;

using Windows.ApplicationModel.Appointments;

namespace CalDav.Helpers
{
    public static class CalDavHelper
    {
        private static AppointmentStore _store;

        private static async Task<AppointmentStore> getStore()
        {
            return _store ?? (_store = await AppointmentManager.RequestStoreAsync(AppointmentStoreAccessType.AppCalendarsReadWrite));
        }
        public static async Task RemoveServer(CalDavServer server)
        {
            var store = await getStore();
            using (var db = new CalDavContext())
            {
                server = await db.FindAsync<CalDavServer>(server.Id);
                if (server.Calendars != null)
                {
                    foreach(var calendar in server.Calendars)
                    {
                        var appCal = await GetAppointmentCalendar(calendar);
                        await appCal?.DeleteAsync();
                    }
                    db.RemoveRange(server.Calendars);
                    server.Calendars.Clear();
                }
                db.Remove(server);
                await db.SaveChangesAsync();
            }
        }

        public static async Task<CalDavServer> AddServer(string host, string user, string password)
        {
            var result = new CalDavServer
            {
                Host = host,
                Username = user,
                Password = password
            };
            using (var db = new CalDavContext())
            {
                await db.Servers.AddAsync(result);
                await db.SaveChangesAsync();
            }
            await ValidateServer(result);
            return result;
        }

        public static async Task<bool> ValidateServer(CalDavServer server)
        {
            using (var db = new CalDavContext())
            {
                server.UserDir = null;
                server.CalendarHomeSet = null;
                if (server.Calendars != null)
                {
                    db.RemoveRange(server.Calendars);
                    server.Calendars.Clear();
                }
                await db.SaveChangesAsync();
                try
                {
                    using (var client = new CalDavClient(server))
                    {
                        await client.Prepare();
                        return true;
                    }
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        public static async Task<bool> SynchronizeCalendar(CalDavCalendar calendar)
        {
            try
            {
                using (var db = new CalDavContext())
                {
                    db.Attach(calendar);
                    using (var client = new CalDavClient(calendar.Server))
                    {
                        await client.Prepare();
                        await client.SyncCalendar(calendar);
                        return true;
                    }
                }
            } catch (Exception)
            {
                return false;
            }
        }

        public static async Task SetCalendarSyncState(CalDavCalendar calendar, bool state)
        {
            var appCal = await GetAppointmentCalendar(calendar);
            if (!state)
            {
                await appCal?.DeleteAsync();
                calendar.Initialized = false;
                calendar.SyncToken = null;
            } else
            {
                if (appCal == null)
                {
                    appCal = await (await getStore()).CreateAppointmentCalendarAsync(calendar.Displayname);
                    appCal.CanCancelMeetings = false;
                    appCal.CanCreateOrUpdateAppointments = false;
                    appCal.CanForwardMeetings = false;
                    appCal.CanNotifyInvitees = false;
                    appCal.OtherAppReadAccess = AppointmentCalendarOtherAppReadAccess.Full;
                    appCal.OtherAppWriteAccess = AppointmentCalendarOtherAppWriteAccess.Limited;
                    calendar.SyncToken = null;
                    calendar.Initialized = false;
                }
                appCal.DisplayName = calendar.Displayname;
                appCal.RemoteId = calendar.RemoteIdentifier;
                calendar.LocalId = appCal.LocalId;
                await appCal.SaveAsync();
            }
            calendar.ShouldSync = state;
            using (var db = new CalDavContext())
            {
                db.Update(calendar);
                await db.SaveChangesAsync();
            }
        }

        public static async Task<CalDavCalendar> GetCalendar(AppointmentCalendar cal)
        {
            using (var db = new CalDavContext())
            {
                return await db.Calendars.FirstOrDefaultAsync(c => c.RemoteIdentifier == cal.RemoteId);
            }
        }

        public static async Task<AppointmentCalendar> GetAppointmentCalendar(CalDavCalendar cal)
        {
            if (cal == null)
                return null;
            var calendars = await (await getStore()).FindAppointmentCalendarsAsync(FindAppointmentCalendarsOptions.IncludeHidden);
            return calendars.FirstOrDefault(c => c.RemoteId == cal.RemoteIdentifier);
        }
    }
}
