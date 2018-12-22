using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Proxies;

using System.Diagnostics;

using Windows.ApplicationModel.Appointments;

namespace CalDav.Extensions
{
    public static class CalendarExtension
    {
        public static Appointment ToAppointment(this CalendarEvent self)
        {
            var result = new Appointment();
            result.AllDay = self.IsAllDay;
            result.BusyStatus = ToBusyStatus(self.Status);
            result.Details = self.Description ?? "";
            result.DetailsKind = GetDetailsKind(self.Description);
            result.Duration = self.Duration;
            result.Location = self.Location ?? "";
            result.Recurrence = ToAppRecurrence(self);
            result.Organizer = ToAppOrganizer(self.Organizer);
            result.Reminder = GetReminder(self);
            result.StartTime = self.Start.AsDateTimeOffset;
            result.Subject = self.Summary ?? "";
            result.RoamingId = self.Uid ?? "";
            return result;
        }

        public static CalendarEvent ToEvent(this Appointment self)
        {
            var result = new CalendarEvent {
                IsAllDay = self.AllDay,
                Status = self.BusyStatus.ToStatus(),
                Description = self.Details,
                Duration = self.Duration,
                Location = self.Location,
                RecurrenceRules = self.Recurrence.ToRules(),
                Organizer = self.Organizer.ToOrganizer(),
                Summary = self.Subject,
                DtStart = new CalDateTime(self.StartTime.UtcDateTime),
                DtEnd = new CalDateTime(self.StartTime.UtcDateTime.Add(self.Duration)),
                Uid = self.RoamingId
            };
            if (self.Reminder.HasValue)
                result.Alarms.Add(self.GetAlarm());
            return result;
        }

        public static Alarm GetAlarm(this Appointment self)
        {
            if (!self.Reminder.HasValue)
                return null;
            var alarm = new Alarm();
            alarm.Trigger = new Trigger();
            alarm.Trigger.Duration = self.Reminder;
            return alarm;
        }

        public static string ToStatus(this AppointmentBusyStatus self)
        {
            return self == AppointmentBusyStatus.Free ? "transparent" : "opaque";
        }

        public static TimeSpan? GetReminder(this CalendarEvent self)
        {
            if (self.Alarms == null)
                return null;
            if (self.Alarms.Count == 0)
                return null;
            return self.Start.Date - self.Alarms.FirstOrDefault().Trigger.DateTime.Date;
        }

        public static AppointmentOrganizer ToAppOrganizer(this Organizer self)
        {
            if (self == null)
                return null;
            var result = new AppointmentOrganizer
            {
                Address = self.Value.ToString(),
                DisplayName = self.CommonName
            };
            return result;
        }

        public static Organizer ToOrganizer(this AppointmentOrganizer self)
        {
            if (self == null)
                return null;
            return new Organizer
            {
                CommonName = self.DisplayName,
                Value = new Uri(self.Address)
            };
        }

        public static List<RecurrencePattern> ToRules(this AppointmentRecurrence self)
        {
            if (self == null)
                return new List<RecurrencePattern>();
            var result = new RecurrencePattern();
            result.Interval = (int)self.Interval;
            result.ByMonthDay.Add((int)self.Day);
            result.ByMonth.Add((int)self.Month);
            result.Until = self.Until?.UtcDateTime ?? DateTime.UtcNow.AddYears(20);
            var year = DateTime.Now.Year;
            result.ByYearDay = new List<int> { new DateTime(year, (int)self.Month, (int)self.Day).DayOfYear };

            switch (self.Unit)
            {
                case AppointmentRecurrenceUnit.Daily:
                    result.Frequency = FrequencyType.Daily;
                    break;
                case AppointmentRecurrenceUnit.MonthlyOnDay:
                    result.Frequency = FrequencyType.Monthly;
                    break;
                case AppointmentRecurrenceUnit.YearlyOnDay:
                    result.Frequency = FrequencyType.Yearly;
                    break;
                case AppointmentRecurrenceUnit.Weekly:
                    result.Frequency = FrequencyType.Weekly;
                    break;
                default:
                    return null;
            }

            return new List<RecurrencePattern>{result};
        }

        public static AppointmentRecurrence ToAppRecurrence(this CalendarEvent self)
        {
            if (self == null)
                return null;
            var rule = self.RecurrenceRules.FirstOrDefault();
            if (rule == null)
                return null;
            var result = new AppointmentRecurrence();
            
            result.Interval = (uint)rule.Interval;
            
            if (rule.Until.Year != 1)
            {
                result.Until = rule.Until;
            } else
            {
                if (rule.Count > 0)
                    result.Occurrences = (uint)rule.Count;
                else
                    result.Occurrences = null;
            }

            switch (rule.Frequency)
            {
                case FrequencyType.Daily:
                    result.Unit = AppointmentRecurrenceUnit.Daily;
                    result.DaysOfWeek = rule.ByDay.ToAppDaysOfWeek(self.DtStart.Date.DayOfWeek.ToAppDayOfWeek());
                    break;
                case FrequencyType.Weekly:
                    result.Unit = AppointmentRecurrenceUnit.Weekly;
                    result.DaysOfWeek = rule.ByDay.ToAppDaysOfWeek(self.DtStart.Date.DayOfWeek.ToAppDayOfWeek());
                    break;
                case FrequencyType.Monthly:
                    result.DaysOfWeek = rule.ByDay.ToAppDaysOfWeek(AppointmentDaysOfWeek.None);
                    if (result.DaysOfWeek != AppointmentDaysOfWeek.None)
                        result.Unit = AppointmentRecurrenceUnit.MonthlyOnDay;
                    else
                    {
                        result.Unit = AppointmentRecurrenceUnit.Monthly;
                        if (rule.ByMonthDay.Count > 0)
                            result.Day = (uint)rule.ByMonthDay[0];
                        else
                            result.Day = (uint)self.DtStart.AsDateTimeOffset.Day;
                    }
                    break;
                case FrequencyType.Yearly:
                    result.DaysOfWeek = rule.ByDay.ToAppDaysOfWeek(AppointmentDaysOfWeek.None);
                    if (result.DaysOfWeek != AppointmentDaysOfWeek.None)
                        result.Unit = AppointmentRecurrenceUnit.YearlyOnDay;
                    else
                    {
                        result.Unit = AppointmentRecurrenceUnit.Yearly;

                        if (rule.ByYearDay.Count > 0)
                        {
                            var year = DateTime.Now.Year;
                            var help = new DateTime(year, 1, 1).AddDays(rule.ByYearDay[0] - 1);
                            result.Month = (uint)help.Month;
                            result.Day = (uint)help.Day;
                        }
                        if (rule.ByMonthDay.Count > 0)
                            result.Day = (uint)rule.ByMonthDay[0];
                        else
                            result.Day = (uint)self.DtStart.AsDateTimeOffset.Day;
                        if (rule.ByMonth.Count > 0)
                            result.Month = (uint)rule.ByMonth[0];
                        else
                            result.Month = (uint)self.DtStart.AsDateTimeOffset.Month;
                    }
                    break;
                default:
                    return null;
            }
            return result;
        }

        private static AppointmentDaysOfWeek ToAppDaysOfWeek(this List<WeekDay> days, AppointmentDaysOfWeek def = AppointmentDaysOfWeek.None)
        {
            if (days == null || days.Count == 0)
                return def;
            var result = AppointmentDaysOfWeek.None;
            foreach (var day in days)
                result |= day.DayOfWeek.ToAppDayOfWeek();
            return result;
        }

        private static AppointmentDaysOfWeek ToAppDayOfWeek(this DayOfWeek day)
        {
            switch(day)
            {
                case DayOfWeek.Monday:
                    return AppointmentDaysOfWeek.Monday;
                case DayOfWeek.Tuesday:
                    return AppointmentDaysOfWeek.Tuesday;
                case DayOfWeek.Wednesday:
                    return AppointmentDaysOfWeek.Wednesday;
                case DayOfWeek.Thursday:
                    return AppointmentDaysOfWeek.Thursday;
                case DayOfWeek.Friday:
                    return AppointmentDaysOfWeek.Friday;
                case DayOfWeek.Saturday:
                    return AppointmentDaysOfWeek.Saturday;
                case DayOfWeek.Sunday:
                    return AppointmentDaysOfWeek.Sunday;
            }
            return AppointmentDaysOfWeek.None;
        }

        private static AppointmentBusyStatus ToBusyStatus(string str)
        {
            if (str != null && str.ToLower() == "opaque")
                return AppointmentBusyStatus.Busy;
            return AppointmentBusyStatus.Free;
        }

        private static AppointmentDetailsKind GetDetailsKind(string details)
        {
            return AppointmentDetailsKind.PlainText;
        }
    }
}
