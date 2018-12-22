using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;
using Windows.UI.Popups;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

using Microsoft.EntityFrameworkCore;

using CalDav.Models;
using CalDav.Helpers;
using Microsoft.Toolkit.Uwp.UI.Controls;

namespace CalDav.Views
{
    public sealed partial class MainPage : Page, INotifyPropertyChanged 
    {
        public List<CalDavServer> Servers { get; private set; } = new List<CalDavServer>();

        private CalDavContext db;

        public MainPage()
        {
            InitializeComponent();
            db = new CalDavContext();
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

        private void AddButtonClick(object sender, RoutedEventArgs e)
        {
            var server = ServerAdressBox.Text;
            var user = UsernameBox.Text;
            var passwd = PasswordBox.Password;
            var scheduler = TaskScheduler.Current;
            AddServer(server, user, passwd).ContinueWith(async (task) =>
            {
                if (task.Result)
                {
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                     {
                         ServerAdressBox.Text = "";
                         UsernameBox.Text = "";
                         PasswordBox.Password = "";
                         RefreshDataGrid();
                     });
                }
            });
        }

        private async Task<bool> AddServer(string host, string user, string password)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                await new MessageDialog("Host not specified.", "Data missing").ShowAsync();
                return false;
            }
            if (string.IsNullOrWhiteSpace(user))
            {
                await new MessageDialog("User not specified.", "Data missing").ShowAsync();
                return false;
            }
            if (string.IsNullOrWhiteSpace(password))
            {
                await new MessageDialog("Password not specified.", "Data missing").ShowAsync();
                return false;
            }
            if (await db.Servers.AnyAsync(s => s.Host == host && s.Username == user))
            {
                await new MessageDialog("There already is an entry for this server using this username.", "Server already exists").ShowAsync();
                return false;
            }
            await CalDavHelper.AddServer(host, user, password);
            return true;
        }

        private void PageLoaded(object sender, RoutedEventArgs e)
        {
            RefreshDataGrid();
        }

        private async void RefreshDataGrid()
        {
            Servers = await db.Servers.ToListAsync();
            Bindings.Update();
        }

        private async void RowEdited(object sender, DataGridRowEditEndedEventArgs e)
        {
            var index = e.Row.GetIndex();
            if (index >= 0 && index < Servers.Count)
            {
                var server = Servers[index];
                await CalDavHelper.ValidateServer(server, true);
                RefreshDataGrid();
            }
        }
        
        private async void DataGridKeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Delete)
            {
                if (!(sender is DataGrid grid))
                {
                    return;
                }
                foreach (var server in grid.SelectedItems.Cast<CalDavServer>())
                {
                    await CalDavHelper.RemoveServer(server);
                }
                RefreshDataGrid();
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
                    if (index >= 0 && index < Servers.Count)
                    {
                        var server = Servers[index];
                        await CalDavHelper.ValidateServer(server);
                        RefreshDataGrid();
                    }
                    break;
                }
            }
        }

        private async void DeleteClicked(object sender, RoutedEventArgs e)
        {
            for (var vis = sender as DependencyObject; vis != null; vis = VisualTreeHelper.GetParent(vis))
            {
                if (vis is DataGridRow)
                {
                    var row = vis as DataGridRow;
                    var index = row.GetIndex();
                    if (index >= 0 && index < Servers.Count)
                    {
                        var server = Servers[index];
                        await CalDavHelper.RemoveServer(server);
                        RefreshDataGrid();
                    }
                    break;
                }
            }
        }
    }
}
