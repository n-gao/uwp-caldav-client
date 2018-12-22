using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Microsoft.Toolkit.Uwp.UI.Controls;

using System.Diagnostics;

using Microsoft.EntityFrameworkCore;

using CalDav.Models;
using CalDav.Helpers;

namespace CalDav.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class CalendarsPage : Page
    {
        public List<CalDavCalendar> Calendars { get; private set; } = new List<CalDavCalendar>();
        private CalDavContext db;

        public CalendarsPage()
        {
            this.InitializeComponent();
            db = new CalDavContext();
        }

        ~CalendarsPage()
        {            
            db.Dispose();
        }

        private void PageLoaded(object sender, RoutedEventArgs e)
        {
            RefreshCalendars();
        }

        private async void RefreshCalendars()
        {
            using(var db = new CalDavContext())
            {
                Calendars = await db.Calendars.ToListAsync();
                Bindings.Update();
            }
        }

        private async void RowEdited(object sender, DataGridRowEditEndedEventArgs e)
        {
            var index = e.Row.GetIndex();
            if (index >= 0 && index < Calendars.Count)
            {
                var calendar = Calendars[index];
                await CalDavHelper.SetCalendarSyncState(calendar, calendar.ShouldSync);
            }
        }

        private async void RefreshClicked(object sender, RoutedEventArgs e)
        {
            for (var vis = sender as DependencyObject; vis != null; vis = VisualTreeHelper.GetParent(vis))
            {
                if (vis is DataGridRow)
                {
                    var row = vis as DataGridRow;
                    var index = row.GetIndex();
                    if (index >= 0 && index < Calendars.Count)
                    {
                        var cal = Calendars[index];
                        await CalDavHelper.SynchronizeCalendar(cal);
                    }
                    break;
                }
            }
        }
    }
}
