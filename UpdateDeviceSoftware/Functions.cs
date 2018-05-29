#region

using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Hl7.Fhir.Model;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Common.Extensions;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Task = System.Threading.Tasks.Task;

#endregion

namespace UpdateDeviceSoftware
{
	public class Functions
	{
		public static RegistryManager RegistryManager;
		private static readonly string ConnString = ConfigurationManager.AppSettings["connString"];
		private static ServiceClient _client;
		private static string[] _targetDeviceId;
		private static string _documentId;

		private static readonly string Endpoint = ConfigurationManager.AppSettings["endpoint"];
		private static readonly string Database = ConfigurationManager.AppSettings["database"];
		private static readonly string Collection = ConfigurationManager.AppSettings["collection"];

		public static HttpClient HttpClient = new HttpClient();
		public static DocumentClient Client;

		public static async Task<HttpResponseMessage> UpdateSecureTask(
			HttpRequestMessage req,
			DurableOrchestrationClient client,
			ILogger logger)
		{
			// Function name comes from the request URL.
			// Function input comes from the request content.
			dynamic eventData = await req.Content.ReadAsStreamAsync();


			string instanceId = await client.StartNewAsync(nameof(UpdateSecure), input: eventData);

			logger.LogInformation($"Started orchestration with ID = '{instanceId}'.");

			DurableOrchestrationStatus status;

			while (true)

			{
				status = await client.GetStatusAsync(instanceId);

				logger.LogInformation($"Status: {status.RuntimeStatus}, Last update: {status.LastUpdatedTime}.");


				if (status.RuntimeStatus == OrchestrationRuntimeStatus.Completed ||
				    status.RuntimeStatus == OrchestrationRuntimeStatus.Failed ||
				    status.RuntimeStatus == OrchestrationRuntimeStatus.Terminated)
					break;


				await Task.Delay(TimeSpan.FromSeconds(2));
			}


			logger.LogInformation($"Output: {status.Output}");

			return client.CreateCheckStatusResponse(req, instanceId);

		}

	
		

	
		public static async Task<string> UpdateSecure(
			[OrchestrationTrigger] DurableOrchestrationContext context)
		{
			_targetDeviceId = context.GetInput<string[]>();

			if (Client == null) Client = await GetSecureDocumentClient();

			var deviceId = _targetDeviceId[0];
			var updateVersionId = _targetDeviceId[1];
			var specType = _targetDeviceId[2];



			// Get the DeviceComponent Document
			var doc = await context.CallActivityAsync<dynamic>("GetDoc", deviceId);


			_documentId = doc.Id;


			var binder = new Binder();
			binder.BindingData.Add("documentId", _documentId);
			binder.BindingData.Add("attachmentId", updateVersionId);

			// Get the Attachment
			string attachment = await context.CallActivityAsync<dynamic>(nameof(GetAttachment), binder);


			var result = await context.CallActivityAsync<CloudToDeviceMethodResult>(nameof(StartFirmwareUpdate), attachment);
			if (result.Status != 0)
				return result.Status.ToString();


			var status = await context.CallActivityAsync<string>(nameof(QueryTwinFwUpdateReportedSecure), DateTime.Now);

			var instant = new DateTimeOffset(DateTime.Parse(status));

			var spec = new DeviceComponent.ProductionSpecificationComponent
			{
				ComponentId = {Value = Guid.NewGuid().ToString()},
				ProductionSpec = updateVersionId
			};
			var coding = new Coding(null, specType, specType.ToUpper());
			spec.SpecType.Coding.Add(coding);

			var ps = new DeviceComponent().ProductionSpecification;
			ps.Add(spec);

			// Update the LastSystemChange value
			var deviceComponentUpdate = new DeviceComponent
			{
				Id = _documentId,
				Type = doc.Type,
				Text = doc.Text,
				Source = doc.Source,
				Parent = doc.Parent,
				FhirComments = doc.FhirComments,
				ImplicitRules = doc.ImplicitRules,
				Identifier = doc.Identifier,
				OperationalStatus = doc.OperationalStatus,
				Contained = doc.Contained,
				LastSystemChange = instant,
				MeasurementPrinciple = doc.MeasurementPrinciple,
				ParameterGroup = doc.ParameterGroup,
				MeasurementPrincipleElement = doc.MeasurementPrincipleElement,
				LanguageCode = doc.LanguageCode,
				ProductionSpecification = ps,
				Language = doc.Language
			};

			var json = JsonConvert.SerializeObject(deviceComponentUpdate);


			// Update the Device Docuument
			var upDate = await context.CallActivityAsync<HttpResponseMessage>(nameof(UpdateDoc), json);

			if (!upDate.IsSuccessStatusCode) return upDate.StatusCode.ToString();

			var cal = new DeviceMetric.CalibrationComponent
			{
				Type =
					null, //values: unspecified, ofset, gin, two-point see https://www.hl7.org/fhir/valueset-metric-calibration-type.html
				State =
					null,      // values: not-calibrated,  calibration-required, calibrated, unspecified see https://www.hl7.org/fhir/valueset-metric-calibration-state.html
				Time = instant // value: DateTimeOffset
			};
			var calibration = new DeviceMetric().Calibration;
			calibration.Add(cal);


			var deviceMetric = new DeviceMetric
			{
				OperationalStatus = doc.Result.OperationalStatus,
				Category = doc.Result.Category,
				Color = doc.Result.Color,
				Contained = doc.Result.Contained,
				Type = doc.Result.Type,
				Unit = doc.Result.Unit,
				Source = doc.Result.Source,
				Parent = doc.Result.Parent,
				MeasurementPeriod = null,
				Calibration = calibration
			};

			json = JsonConvert.SerializeObject(deviceMetric);

			upDate = await context.CallActivityAsync<HttpResponseMessage>(nameof(UpdateDoc), json);


			return upDate.StatusCode.ToString();
		}

		public static async Task<string> QueryTwinFwUpdateReportedSecure([ActivityTrigger] DateTime startTime)
		{
			var lastUpdated = startTime;

			var deviceId = _targetDeviceId[0];

			while (true)

			{
				

				var twin = await RegistryManager.GetTwinAsync(deviceId);


				if (twin.Properties.Reported.GetLastUpdated().ToUniversalTime() > lastUpdated.ToUniversalTime())
				{
					lastUpdated = twin.Properties.Reported.GetLastUpdated().ToUniversalTime();


					var status = twin.Properties.Reported["iothubDM"]["firmwareUpdate"]["status"].Value;
					if (status == "downloadFailed" || status == "applyFailed" || status == "applyComplete") return status;

					return lastUpdated.ToString(CultureInfo.InvariantCulture);
				}

				await Task.Delay(500);
			}
		}

		public static async Task<CloudToDeviceMethodResult> StartFirmwareUpdate([ActivityTrigger] string payLoad)
		{
			var deviceId = _targetDeviceId[0];

			using (var webClient = new WebClient())
			{
				var content = webClient.DownloadData(payLoad);
				using (var stream = new MemoryStream(content))
				{
					var file = JsonConvert.SerializeObject(stream);

					_client = ServiceClient.CreateFromConnectionString(ConnString);
					var method = new CloudToDeviceMethod("firmwareUpdate") {ResponseTimeout = TimeSpan.FromSeconds(30)};
					method.SetPayloadJson(file);

					var result = await _client.InvokeDeviceMethodAsync(deviceId, method);

					return result;
				}
			}
		}

		public static Task<dynamic> GetDoc([ActivityTrigger] string id)
		{
			try
			{
				var feedOptions = new FeedOptions {MaxItemCount = 1};

				var doc = Client.CreateDocumentQuery(UriFactory.CreateDocumentCollectionUri(Database, Collection),
					$"SELECT * FROM d WHERE d.resourceType = \'DeviceComponent\' AND d.source.reference = \' Device/\'{id}",
					feedOptions);

				var document = doc.FirstOrDefault();


				return document;
			}
			catch (DocumentClientException)
			{
				return null;
			}
		}

		public static async Task<HttpResponseMessage> UpdateDoc([ActivityTrigger] dynamic document)
		{
			string id = document.id;
			object doc = document.doc;


			try
			{
				await Client.UpsertDocumentAsync(UriFactory.CreateDocumentUri(Database, Collection, id), doc);

				return new HttpResponseMessage(HttpStatusCode.OK);
			}
			catch (DocumentClientException e)
			{
				var resp = new HttpResponseMessage
				{
					StatusCode = HttpStatusCode.BadRequest,
					Content = new StringContent(e.Message)
				};
				return resp;
			}
		}

		public static async Task<dynamic> GetAttachment([ActivityTrigger] Binder binder)
		{
			try
			{
				var document = binder.BindingData.GetValueOrDefault("documentId").ToString();
				var attachment = binder.BindingData.GetValueOrDefault("attachmentId").ToString();

				var result =
					await Client.ReadAttachmentAsync(UriFactory.CreateAttachmentUri(Database, Collection, document, attachment));


				return result.Resource.MediaLink;
			}
			catch (DocumentClientException e)
			{
				var resp = new HttpResponseMessage
				{
					StatusCode = HttpStatusCode.BadRequest,
					Content = new StringContent(e.Message)
				};
				return resp;
			}
		}

		private static async Task<DocumentClient> GetSecureDocumentClient()
		{
			var azureServiceTokenProvider = new AzureServiceTokenProvider();
			var endpointUrl = Endpoint;
			var keyVaultClient =
				new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback),
					HttpClient);
			var key = await keyVaultClient.GetSecretAsync(ConfigurationManager.AppSettings["keyVaultAccessUri"]);
			return new DocumentClient(new Uri(endpointUrl), key.Value);
		}

	}
}