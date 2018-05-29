#region

using System.Configuration;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

#endregion

namespace UpdateDeviceSoftware
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			using (var loggerFactory = new LoggerFactory())
			{
				var config = new JobHostConfiguration
				{
					DashboardConnectionString = "",
					StorageConnectionString = ConfigurationManager.AppSettings["AzureWebJobsStorage"]
				};


				var instrumentationKey =
					ConfigurationManager.AppSettings["APPINSIGHTS_INSTRUMENTATIONKEY"];

				config.LoggerFactory = loggerFactory.AddApplicationInsights(instrumentationKey, null).AddConsole();

				config.UseDurableTask(new DurableTaskExtension
				{
					HubName = "UpdateSoftware"
				});

				var host = new JobHost(config);
				host.RunAndBlock();
			}
		}
	}
}