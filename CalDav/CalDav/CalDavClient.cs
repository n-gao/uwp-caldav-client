using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.Xml;
using System.Xml.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Diagnostics;
using Ical.Net;
using Ical.Net.DataTypes;
using Ical.Net.CalendarComponents;
using Ical.Net.Serialization;
using Microsoft.EntityFrameworkCore;

using Windows.ApplicationModel.Appointments;

using CalDav.Models;
using CalDav.Extensions;
using CalDav.Helpers;

namespace CalDav.CalDav
{
    public partial class CalDavClient : IDisposable
    {
        public NetworkCredential Credentials { get; private set; }
        public AppointmentStore LocalStore { get; private set; }
        public CalDavServer Server { get; set; }
        private CalDavContext Db { get; set; }
        public bool IsReady => LocalStore != null;

        public CalDavClient(CalDavServer server)
        {
            Server = server ?? throw new ArgumentNullException("Server must not be null");
            Db = new CalDavContext();
        }

        public void Dispose()
        {
            Db.Dispose();
        }

        public CalDavClient(string host, string username, string password)
        {
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                throw new ArgumentNullException("Some arguments are null or empty.");
            using (var db = new CalDavContext())
            {
                Server = db.Servers.FirstOrDefault(s => s.Host == host && s.Username == username);
                if (Server != null)
                {
                    Server = new CalDavServer
                    {
                        Host = host,
                        Username = username,
                        Password = password
                    };
                    db.Servers.Add(Server);
                    db.SaveChangesAsync();
                }
            }
            Db = new CalDavContext();
        }

        public async Task Prepare()
        {
            Credentials = new NetworkCredential(Server.Username, Server.Password);

            if (Server.UserDir == null)
                Server.UserDir = await GetUserDir();
            if (Server.CalendarHomeSet == null)
                Server.CalendarHomeSet = await GetCalendarHomeSet();
            Db.Update(Server);
            await Db.SaveChangesAsync();

            LocalStore = await AppointmentManager.RequestStoreAsync(AppointmentStoreAccessType.AppCalendarsReadWrite);
            LocalStore.ChangeTracker.Enable();

            await GetCalendars();
        }

        public async Task PutAppointment(CalDavCalendar calendar, Appointment appointment, string href = null, string etag = null)
        {
            href = string.IsNullOrEmpty(href) ? Guid.NewGuid().ToString() : href;
            var toPut = new Calendar();
            toPut.Events.Add(appointment.ToEvent());
            var serializer = new CalendarSerializer();
            var iCal = serializer.SerializeToString(toPut);
            var (code, content, headers) = await Request(calendar.Href + $"{href}.ics", "PUT", iCal, "text/calendar", etag: etag);
        }

        public async Task DeleteAppointment(CalDavCalendar calendar, string href, string etag)
        {
            if (string.IsNullOrEmpty(href))
                return; 
            var (code, content, headers) = await Request(calendar.Href + $"{href}.ics", "DELETE", "", etag: etag);
        }

        public async Task UploadChanges()
        {
            IReadOnlyList<AppointmentStoreChange> changes;
            var changeReader = LocalStore.ChangeTracker.GetChangeReader();
            while ((changes = await changeReader.ReadBatchAsync()).Count > 0)
            {
                foreach (var change in changes)
                {
                    CalDavCalendar cal = null;
                    CalDavAppointment app = null;
                    Debug.WriteLine(change.ChangeType);
                    if ((int)change.ChangeType < 3)
                    {
                        cal = await CalDavHelper.GetCalendar(await LocalStore.GetAppointmentCalendarAsync(change.Appointment.CalendarId));
                        app = cal.Appointments.FirstOrDefault(a => a.Href == change.Appointment.RoamingId && a.Calendar == cal);
                    }
                    switch (change.ChangeType)
                    {
                        case AppointmentStoreChangeType.AppointmentCreated:
                            if (string.IsNullOrEmpty(change.Appointment.RoamingId))
                            {
                                await PutAppointment(cal, change.Appointment, app.Etag);
                                await change.AppointmentCalendar.DeleteAppointmentAsync(change.Appointment.LocalId);
                            }
                            break;
                        case AppointmentStoreChangeType.AppointmentDeleted:
                            var href = change.Appointment.RoamingId;
                            var localId = change.Appointment.LocalId;
                            if (Db.Appointments.Any(a => a.Href == href && a.LocalId == localId && a.Calendar == cal))
                                await DeleteAppointment(cal, href, app.Etag);
                            break;
                        case AppointmentStoreChangeType.AppointmentModified:
                            await PutAppointment(cal, change.Appointment, change.Appointment.RoamingId);
                            break;
                    }
                    changeReader.AcceptChangesThrough(change);
                }
                changeReader.AcceptChanges();
            }
            await SyncCalendars();
        }

        public async Task SyncCalendars()
        {
            Db.Attach(Server);
            foreach(var cal in Server.Calendars)
            {
                await SyncCalendar(cal);
            }
        }

        public async Task SyncCalendar(CalDavCalendar calendar)
        {
            if (!calendar.Initialized)
            {
                await UpdateSyncToken(calendar);
                await GetCalendarEntries(calendar);
            }

            var (code, content, headers) = await Request(calendar.Href, "REPORT", GetSyncQuery(calendar.SyncToken).ToString());


            var status = content.Element(xMultistatus);

            foreach (var response in status.Elements(xResponse))
            {
                var href = response.Element(xHref).Value;
                var propStat = response.Element(xPropStat);
                var st = response.Element(xStatus)?.Value ?? propStat.Element(xStatus)?.Value;
                if (st.Contains("200"))
                {
                    var prop = propStat.Element(xProp);
                    var ics = prop.Element(xCalendarData).Value;
                    var etag = prop.Element(xGetETag).Value;
                    // TODO ADD
                    await AddAppointment(calendar, href, etag, ics);
                }
                else
                {
                    // TODO DELETE
                    await RemoveAppointment(calendar, href);
                }
            }

            calendar.SyncToken = status.Element(xSyncToken).Value;
        }

        private async Task AddAppointment(CalDavCalendar calendar, string href, string etag, string ics)
        {
            var cal = await CalDavHelper.GetAppointmentCalendar(calendar);

            // Ensure no duplicates
            await RemoveAppointment(calendar, href);

            var parsed = Calendar.Load(ics).Events[0];
            var appointment = parsed.ToAppointment();
            await cal.SaveAppointmentAsync(appointment);

            await Db.Appointments.AddAsync(new CalDavAppointment
            {
                Calendar = calendar,
                CalendarId = calendar.Id,
                Href = href,
                Etag = etag,
                LocalId = appointment.LocalId
            });
            await Db.SaveChangesAsync();
        }

        private async Task RemoveAppointment(CalDavCalendar calendar, string href)
        {
            var cal = await CalDavHelper.GetAppointmentCalendar(calendar);
            try
            {
                var app = await Db.Appointments.FirstAsync(a => a.Calendar == calendar && a.Href == href);
                await cal.DeleteAppointmentAsync(app.LocalId);
                Db.Appointments.Remove(app);
                await Db.SaveChangesAsync();
            }
            catch (Exception) { }
        }

        private async Task GetCalendarEntries(CalDavCalendar calendar)
        {
            var (code, content, headers) = await Request(calendar.Href, "REPORT", CalendarQuery.ToString());

            foreach (var resp in content
                .Element(xMultistatus)
                .Elements(xResponse))
            {
                var href = resp.Element(xHref).Value;
                var prop = resp.Element(xPropStat).Element(xProp);
                var etag = prop.Element(xGetETag).Value;
                var ics = prop.Element(xCalendarData).Value;
                await AddAppointment(calendar, href, etag, ics);
            }

            calendar.Initialized = true;
            Db.Update(calendar);
            await Db.SaveChangesAsync();
        }

        private async Task UpdateSyncToken(CalDavCalendar calendar)
        {
            var (code, content, headers) = await Request(calendar.Href, "PROPFIND", GetSyncToken.ToString());

            var token = content.Element(xMultistatus)
                .Element(xResponse)
                .Element(xPropStat)
                .Element(xProp)
                .Element(xSyncToken)
                .Value;

            calendar.SyncToken = token;
            Db.Update(calendar);
            await Db.SaveChangesAsync();
        }
         
        private async Task GetCalendars()
        {
            var (code, content, headers) = await Request(Server.CalendarHomeSet, "PROPFIND", CalendarSearch.ToString(), depth: 1);
            
            foreach (var response in content.Element(xMultistatus).Elements(xResponse))
            {
                string href = response.Element(xHref).Value;
                string displayname = null, ctag = null, syncToken = null;
                bool supported = false;
                foreach (var stat in response.Elements(xPropStat))
                {
                    var prop = stat.Element(xProp);
                    var xname = prop.Element(xDisplayname);
                    var xtoken = prop.Element(xSyncToken);
                    var xctag = prop.Element(xGetCTag);

                    displayname = xname == null || xname.IsEmpty ? displayname : xname.Value;
                    syncToken = xtoken == null || xtoken.IsEmpty ? syncToken : xtoken.Value;
                    ctag = xctag == null || xctag.IsEmpty ? ctag : xctag.Value;
                    
                    supported = supported ||
                        prop.Elements(xSupportedCalendarComponent)
                        .Any(c => c.Elements(xComp)
                            .Any(e => e.Attribute(xName).Value == "VEVENT")
                        );
                }
                if (supported)
                {
                    var calendar = await Db.Calendars.FirstOrDefaultAsync(c => c.ServerId == Server.Id && c.Href == href);
                    if (calendar == null)
                    {
                        calendar = new CalDavCalendar
                        {
                            Server = Server,
                            ServerId = Server.Id,
                            Href = href,
                            Ctag = ctag,
                            SyncToken = syncToken,
                            Displayname = displayname,
                            LocalId = null
                        };
                        await Db.Calendars.AddAsync(calendar);
                        await Db.SaveChangesAsync();
                    }
                }
            }
        }

        private async Task<string> GetCalendarHomeSet()
        {
            var (code, content, headers) = await Request(Server.UserDir, "PROPFIND", CalendarDiscovery.ToString(), depth: 0);

            var response = content.Element(xMultistatus).Element(xResponse);
            foreach (var propstat in response.Elements(xPropStat))
            {
                if (!propstat.Element(xStatus).Value.Contains("200"))
                    continue;
                var cal = propstat.Element(xProp).Element(xCalendarHomeSet);
                if (cal != null)
                {
                    return cal.Element(xHref).Value;
                }
            }
            return null;
        }

        private async Task<string> GetUserDir()
        {
            var (code, content, headers) = await Request("/", "PROPFIND", UserDiscovery.ToString(), depth: 0);

            var response = content.Element(xMultistatus).Element(xResponse);
            foreach (var propstat in response.Elements(xPropStat))
            {
                if (!propstat.Element(xStatus).Value.Contains("200"))
                    continue;
                var curr = propstat.Element(xProp).Element(xCurrentUserPrincipal);
                if (curr != null)
                {
                    return curr.Element(xHref).Value;
                }
            }
            return null;
        }

        private async Task<(HttpStatusCode, XDocument, WebHeaderCollection)> Request(string destination, string method, string content, string contentType = "text/xml", int depth = 0, string etag = null)
        {
            var req = WebRequest.CreateHttp(Server.Host + destination);
            req.Credentials = Credentials;
            req.Method = method;
            req.ContentType = contentType;
            req.ContentLength = content.Length;
            req.Headers["Depth"] = depth.ToString();
            if (!string.IsNullOrEmpty(etag))
            {
                req.Headers["If-Match"] = etag;
            }

            if (Credentials != null)
            {
                var b64 = Credentials.UserName + ":" + Credentials.Password;
                b64 = Convert.ToBase64String(Encoding.Default.GetBytes(b64));
                req.Headers[HttpRequestHeader.Authorization] = "Basic " + b64;
            }

            using (var stream = await req.GetRequestStreamAsync())
            {
                using (var wrt = new StreamWriter(stream))
                {
                    await wrt.WriteAsync(content);
                }
            }
            HttpWebResponse response;
            try
            {
                response = await req.GetResponseAsync() as HttpWebResponse;
            } catch(WebException e)
            {
                response = e.Response as HttpWebResponse;
                if (response == null)
                {
                    throw e;
                }
            }
            string reqContent = "";
            using (var stream = response.GetResponseStream())
            {
                using (var read = new StreamReader(stream))
                {
                    reqContent = await read.ReadToEndAsync();
                }
            }

            try
            {
                var xContent = XDocument.Parse(reqContent);
                return (response.StatusCode, xContent, response.Headers);
            }
            catch (Exception)
            {
                return (response.StatusCode, null, response.Headers);
            }
        }
    }
}
