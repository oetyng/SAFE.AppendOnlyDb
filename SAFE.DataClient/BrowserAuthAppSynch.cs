using System;
using System.Threading;
using System.Threading.Tasks;
using SAFE.Data.Client.Auth;

namespace SAFE.Data.Client
{
    /// <summary>
    /// Used when authenticating via SAFEBrowser.
    ///
    /// 1. First running instance manages registration of URL schema and
    /// runs the task passed in as param (would be the actual application,
    /// which would optionally start browser auth flow).
    ///
    /// 2. If browser auth flow is chosen, second instance is started by the SAFEBrowser and
    /// sends auth token passed from it, to the first running instance.
    /// </summary>
    public class BrowserAuthAppSynch
    {
        Mutex _mutex;
        bool _firstApplicationInstance;

        public BrowserAuthAppSynch()
        {
        }

        public async Task RunAsync(string[] args, AppInfo appInfo, Func<Task> whenFirstInstance)
        {
            if (IsApplicationFirstInstance(appInfo.Name))
            {
                // args[0] is always the path to the application
                // update system registry
                AuthConfig.WinDesktopUseBrowserAuth(appInfo, args[0]);

                await whenFirstInstance();
            }
            else
            {
                // We are not the first instance, send the argument received from browser auth, to the currently running instance
                if (args.Length >= 2)
                {
                    var ack = InterProcessCom.SendAuthResponse(args[1]);

                    Console.WriteLine(ack);
                    Console.WriteLine($"Switch back to your application..");
                    Thread.Sleep(1000); // allow a short moment to percieve the above writelines
                }

                // Close app
                return;
            }
        }

        /// <summary>
        /// We want to use this as the SAFEBrowser instantiates a second instance
        /// of the running application, as to pass it the auth token. This is because
        /// it has no way to communicate with the first running instance, that invoked the
        /// browser in the first place. So the second running instance does this instead.
        /// </summary>
        bool IsApplicationFirstInstance(string appName)
        {
            // Allow for multiple runs but only try and get the mutex once
            if (_mutex == null)
                _mutex = new Mutex(true, appName, out _firstApplicationInstance);

            return _firstApplicationInstance;
        }
    }
}