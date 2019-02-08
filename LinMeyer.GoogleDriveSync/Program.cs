using Google.Apis.Drive.v3;
using LinMeyer.GoogleDriveSync.Sync;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.IO;

namespace LinMeyer.GoogleDriveSync
{
    class Program
    {
        private static AppSettings _settings;

        static void Main(string[] args)
        {
            _settings = GetAppSettings();

            var syncer = new Syncronizer(new SyncConfig
            {
                ApplicationName = _settings.GoogleApplicationName,
                CredentialsPath = _settings.GoogleCredentialsFilePath,
                GoogleDriveFolder = _settings.GoogleDriveFolder
            });

            syncer.Go();

            // Pause app at the end until they close
            Console.ReadLine();
        }

        public static AppSettings GetAppSettings()
        {
            // Build the config from the appsettings file
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            var rootConfig = configBuilder.Build();
            // Get the AppSettings section
            var appSettingsSection = rootConfig.GetSection("AppSettings");
            // Serialize to an object
            var appSettings = appSettingsSection.Get<AppSettings>();
            return appSettings;
        }
    }
}
