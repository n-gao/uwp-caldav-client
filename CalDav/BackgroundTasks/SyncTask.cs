using System;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;

using Windows.ApplicationModel.Background;
using Windows.System.Threading;

using Microsoft.EntityFrameworkCore;

using CalDav.Models;
using CalDav.CalDav;

namespace CalDav.BackgroundTasks
{
    public sealed class SyncTask : BackgroundTask
    {
        public static string Message { get; set; }

        private volatile bool _cancelRequested = false;
        private IBackgroundTaskInstance _taskInstance;
        private BackgroundTaskDeferral _deferral;

        public override void Register()
        {
            var taskName = GetType().Name;
            var taskRegistration = BackgroundTaskRegistration.AllTasks.FirstOrDefault(t => t.Value.Name == taskName).Value;

            if (taskRegistration == null)
            {
                var builder = new BackgroundTaskBuilder()
                {
                    Name = taskName
                };

                // TODO WTS: Define the trigger for your background task and set any (optional) conditions
                // More details at https://docs.microsoft.com/windows/uwp/launch-resume/create-and-register-an-inproc-background-task
                builder.SetTrigger(new TimeTrigger(15, false));
                builder.AddCondition(new SystemCondition(SystemConditionType.UserPresent));
                builder.AddCondition(new SystemCondition(SystemConditionType.InternetAvailable));

                builder.Register();
            }
        }

        public override Task RunAsyncInternal(IBackgroundTaskInstance taskInstance)
        {
            if (taskInstance == null)
            {
                return null;
            }

            _deferral = taskInstance.GetDeferral();

            return Task.Run(async () =>
            {
                //// TODO WTS: Insert the code that should be executed in the background task here.
                //// This sample initializes a timer that counts to 100 in steps of 10.  It updates Message each time.

                //// Documentation:
                ////      * General: https://docs.microsoft.com/en-us/windows/uwp/launch-resume/support-your-app-with-background-tasks
                ////      * Debug: https://docs.microsoft.com/en-us/windows/uwp/launch-resume/debug-a-background-task
                ////      * Monitoring: https://docs.microsoft.com/windows/uwp/launch-resume/monitor-background-task-progress-and-completion

                //// To show the background progress and message on any page in the application,
                //// subscribe to the Progress and Completed events.
                //// You can do this via "BackgroundTaskService.GetBackgroundTasksRegistration"

                _taskInstance = taskInstance;

                Debug.WriteLine("Syncing...");
                using (var db = new CalDavContext())
                {
                    var servers = await db.Servers.ToListAsync();
                    foreach (var server in servers)
                    {
                        using (var client = new CalDavClient(server))
                        {
                            try
                            {
                                await client.Prepare();
                                foreach (var cal in server.Calendars.Where(c => c.ShouldSync))
                                {
                                    await client.SyncCalendar(cal);
                                }
                            } catch (Exception) { }
                        }
                    }
                }
                _deferral?.Complete();
            });
        }

        public override void OnCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            _cancelRequested = true;

           // TODO WTS: Insert code to handle the cancelation request here.
           // Documentation: https://docs.microsoft.com/windows/uwp/launch-resume/handle-a-cancelled-background-task
        }
    }
}
