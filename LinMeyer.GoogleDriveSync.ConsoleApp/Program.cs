using LinMeyer.GoogleDriveSync.Sync;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;


namespace LinMeyer.GoogleDriveSync.ConsoleApp
{
    class Program
    {
        private static AppSettings _settings;
        private static IServiceProvider _services;

        static void Main(string[] args)
        {
            // Set up app settings
            _settings = GetAppSettings();

            // Configure Serilog
            Log.Logger = new LoggerConfiguration()
                // File logging will be one log file per day, 1 month of logs kept (default), and only errors will be logged
                .WriteTo.File("log-.txt", rollingInterval: RollingInterval.Day, restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Error)
                // Console logging use the default params which will show anything logged, including INFO
                .WriteTo.ColoredConsole()
                .CreateLogger();

            // Use .NET Core DI to add Serilog to the standard Microsoft...ILogger interface
            ConfigureServices(new ServiceCollection());

            StartSync();
        }

        private static void ConfigureServices(IServiceCollection serviceCollection)
        {
            // Add static for SyncConfig which tells the Syncronizer class how to function
            serviceCollection.AddSingleton(_settings.SyncConfig);
            serviceCollection.AddTransient<Syncronizer>();

            // Add Serilog to DI so we can use the standard .NET Core ILogger
            serviceCollection.AddLogging(configure => configure.AddSerilog());

            // Build service provider
            _services = serviceCollection.BuildServiceProvider();
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
            // Validate app settings
            Validator.ValidateObject(appSettings, new ValidationContext(appSettings), true);
            return appSettings;
        }

        private static void StartSync()
        {
            // Use DI to get the Syncronizer using injected settings and logging
            var sync = _services.GetRequiredService<Syncronizer>();
            sync.Go().Wait();
        }
    }
}
