using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.IO;
using System.Diagnostics;

namespace VRC_OSC_ExternallyTrackedObject
{
    public static class Const
    {
        public const string DefaultConfigName = "config.json";
        public const string DefaultConfigPath = "jangxx\\VRC Tracked Objects";
        public const double MaxRelativeDistance = 2; // this is the max distance the tracker can be away from the controller. this is important for scaling the value since vrchat wants a value between -1 and 1
    }

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            MainWindow wnd = new MainWindow();

            bool initSuccess = wnd.Init();

            if (!initSuccess)
            {
                Shutdown();
                return;
            }


            if (e.Args.Length > 0)
            {
                wnd.LoadConfig(e.Args[0]);
            } else
            {

                var defaultConfigFilePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    Const.DefaultConfigPath,
                    Const.DefaultConfigName
                );

                Debug.WriteLine("Looking for configuration in " + defaultConfigFilePath);

                // try to load default config
                if (File.Exists(defaultConfigFilePath))
                {
                    wnd.LoadConfig(defaultConfigFilePath);
                } else
                {
                    Debug.WriteLine("Default config file doesn't exist. Skipping...");
                }
            }

            wnd.Show();

            wnd.ProcessStartupConfig();
        }
    }
}
