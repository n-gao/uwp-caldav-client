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

namespace CalDav.CalDav
{
    public class CalDavClient
    {
        private static readonly XNamespace xDav = XNamespace.Get("DAV:");
        private static readonly XNamespace xCalDav = XNamespace.Get("urn:ietf:params:xml:ns:caldav");
        private static readonly XNamespace xApple = XNamespace.Get("http://apple.com/ns/ical/");
        private static readonly XNamespace xCardDav = XNamespace.Get("urn:ietf:params:xml:ns:carddav");
        private static readonly XNamespace xCalSer = XNamespace.Get("http://calendarserver.org/ns/");

        private static readonly XName xPropfind = xDav.GetName("propfind");
        private static readonly XName xResponse = xDav.GetName("response");
        private static readonly XName xMultistatus = xDav.GetName("multistatus");
        private static readonly XName xProp = xDav.GetName("prop");
        private static readonly XName xPropStat = xDav.GetName("propstat");
        private static readonly XName xGetETag = xDav.GetName("getetag");
        private static readonly XName xHref = xDav.GetName("href");
        private static readonly XName xDisplayname = xDav.GetName("displayname");
        private static readonly XName xSyncToken = xDav.GetName("sync-token");
        private static readonly XName xSyncCollection = xDav.GetName("sync-collection");
        private static readonly XName xStatus = xDav.GetName("status");
        private static readonly XName xCurrentUserPrincipal = xDav.GetName("current-user-principal");
        private static readonly XName xSyncLevel = xDav.GetName("sync-level");

        private static readonly XName xCalendarHomeSet = xCalDav.GetName("calendar-home-set");
        private static readonly XName xCalendarQuery = xCalDav.GetName("calendar-query");
        private static readonly XName xCalendarData = xCalDav.GetName("calendar-data");
        private static readonly XName xCompFilter = xCalDav.GetName("comp-filter");
        private static readonly XName xFilter = xCalDav.GetName("filter");
        private static readonly XName xSupportedCalendarComponent = xCalDav.GetName("supported-calendar-component-set");
        private static readonly XName xComp = xCalDav.GetName("comp");

        private static readonly XName xName = XName.Get("name");


        private static readonly XName xGetCTag = xCalSer.GetName("getctag");

        private static readonly XAttribute xCalDavAtr = new XAttribute(XNamespace.Xmlns + "c", xCalDav);
        private static readonly XAttribute xDavAtr = new XAttribute(XNamespace.Xmlns + "d", xDav);
        private static readonly XAttribute xCalSerAtr = new XAttribute(XNamespace.Xmlns + "cs", xCalSer);

        private static readonly XDocument CalendarQuery = new XDocument(
            new XElement(xCalendarQuery,
                xCalDavAtr,
                xDavAtr,
                new XElement(xProp,
                    new XElement(xGetETag),
                    new XElement(xCalendarData)
                ),
                new XElement(xFilter,
                    new XElement(xCompFilter,
                        new XAttribute(xName, "VCALENDAR"),
                        new XElement(xCompFilter,
                            new XAttribute(xName, "VEVENT")
                        )
                    )
                )
            )
        );

        private static XDocument GetSyncQuery(string syncToken)
        {
            return new XDocument(
                new XElement(xSyncCollection,
                    xDavAtr,
                    xCalDavAtr,
                    new XElement(xSyncToken,
                        syncToken
                    ),
                    new XElement(xSyncLevel,
                        1
                    ),
                    new XElement(xProp,
                        new XElement(xGetETag),
                        new XElement(xCalendarData)
                    ),
                    new XElement(xFilter,
                        new XElement(xCompFilter,
                            new XAttribute(xName, "VCALENDAR"),
                            new XElement(xCompFilter,
                                new XAttribute(xName, "VEVENT")
                            )
                        )
                    )
                )
            );
        }

        private static XDocument CalendarSearch = new XDocument(
            new XElement(xPropfind,
                xCalSerAtr,
                xDavAtr,
                new XElement(xProp,
                    new XElement(xDisplayname),
                    new XElement(xSyncToken),
                    new XElement(xGetCTag),
                    new XElement(xSupportedCalendarComponent)
                )
            )
        );

        private static XDocument GetSyncToken = new XDocument(
            new XElement(xPropfind,
                xCalSerAtr,
                xDavAtr,
                new XElement(xProp,
                    new XElement(xSyncToken)
                )
            )
        );

        private static readonly XDocument CalendarDiscovery = new XDocument(
            new XElement(
                xPropfind,
                xDavAtr,
                xCalDavAtr,
                new XElement(
                    xProp,
                    new XElement(
                        xCalendarHomeSet
                    )
                )
            )
        );

        private static readonly XDocument UserDiscovery = new XDocument(
            new XElement(
                xPropfind,
                xDavAtr,
                new XElement(
                    xProp,
                    new XElement(
                        xCurrentUserPrincipal
                    )
                )
            )
        );

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
                        cal = await GetCalendar(await LocalStore.GetAppointmentCalendarAsync(change.Appointment.CalendarId));
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
            var cal = await GetAppointmentCalendar(calendar);

            // Ensure no duplicates
            await RemoveAppointment(calendar, href);

            var parsed = Calendar.Load(ics).Events[0];
            var appointment = parsed.ToAppointment();
            await cal.SaveAppointmentAsync(appointment);

            await Db.Appointments.AddAsync(new CalDavAppointment
            {
                Calendar = calendar,
                CalHref = calendar.Href,
                Href = href,
                Etag = etag,
                LocalId = appointment.LocalId
            });
            await Db.SaveChangesAsync();
        }

        private async Task RemoveAppointment(CalDavCalendar calendar, string href)
        {
            var cal = await GetAppointmentCalendar(calendar);
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
                            DisplayName = displayname
                        };
                        var calendars = await LocalStore.FindAppointmentCalendarsAsync(FindAppointmentCalendarsOptions.IncludeHidden);
                        var cal = calendars.FirstOrDefault(c => c.RemoteId == calendar.Href);
                        if (cal == null)
                        {
                            cal = await LocalStore.CreateAppointmentCalendarAsync(displayname);
                            cal.CanCancelMeetings = false;
                            cal.CanCreateOrUpdateAppointments = true;
                            cal.CanForwardMeetings = false;
                            cal.CanNotifyInvitees = false;
                            cal.OtherAppReadAccess = AppointmentCalendarOtherAppReadAccess.Full;
                            cal.OtherAppWriteAccess = AppointmentCalendarOtherAppWriteAccess.Limited;
                        }
                        cal.DisplayName = displayname;
                        cal.RemoteId = calendar.Href;
                        calendar.LocalId = cal.LocalId;
                        await cal.SaveAsync();

                        await Db.Calendars.AddAsync(calendar);
                        await Db.SaveChangesAsync();
                    }
                }
            }
        }

        private async Task<CalDavCalendar> GetCalendar(AppointmentCalendar cal)
        {
            return await Db.Calendars.FirstOrDefaultAsync(c => c.Href == cal.RemoteId && c.ServerId == Server.Id);
        }

        private async Task<AppointmentCalendar> GetAppointmentCalendar(CalDavCalendar cal)
        {
            var calendars = await LocalStore.FindAppointmentCalendarsAsync(FindAppointmentCalendarsOptions.IncludeHidden);
            return calendars.FirstOrDefault(c => c.RemoteId == cal.Href);
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
