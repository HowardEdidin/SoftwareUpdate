using System;
using System.Configuration;
using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SoftwareUpdate
{
    class Program
    {
        static void Main()
        {
            var builder = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).
                AddJsonFile("appsettings.json");

            var appSettingsConfig = builder.Build();

            using (var loggerFactory = new LoggerFactory())
            {

                var config = new JobHostConfiguration
                {
                    DashboardConnectionString = "",
                    StorageConnectionString = appSettingsConfig.GetConnectionString("AzureWebJobsStorage")
                };


                var instrumentationKey =
                    ConfigurationManager.AppSettings["APPINSIGHTS_INSTRUMENTATIONKEY"];

                config.LoggerFactory = loggerFactory.AddApplicationInsights(instrumentationKey, null).AddConsole();

                config.UseDurableTask(new DurableTaskExtension
                {
                    HubName = "UpdateSoftware",
                });

                var host = new JobHost(config);
                host.RunAndBlock();
            }
        }
        
    }
}
