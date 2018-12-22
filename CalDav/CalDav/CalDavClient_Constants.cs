using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml.Linq;

namespace CalDav.CalDav
{
    public partial class CalDavClient : IDisposable
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

        private static XDocument SyncTokenQuery = new XDocument(
            new XElement(xPropfind,
                xCalSerAtr,
                xDavAtr,
                new XElement(xProp,
                    new XElement(xSyncToken)
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
    }
}
