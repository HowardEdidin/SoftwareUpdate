#region

using System;
using System.Fabric;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using DeviceSoftwareUpdate.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.Swagger.Annotations;

#endregion

namespace Web.Controllers
{
    [Route("api/[controller]")]
    public class SoftwareUpdateServiceController : Controller
    {
        private readonly ConfigSettings configSettings;
        private readonly HttpClient httpClient;
        private readonly StatelessServiceContext serviceContext;

        public SoftwareUpdateServiceController(StatelessServiceContext serviceContext, HttpClient httpClient,
            FabricClient fabricClient, ConfigSettings settings)
        {
            this.serviceContext = serviceContext;
            this.httpClient = httpClient;
            configSettings = settings;
        }

        //// GET: api/values
        //[HttpGet]
        //public async Task<IActionResult> GetAsync()
        //{
        //    // the stateful service service may have more than one partition.
        //    // this sample code uses a very basic loop to aggregate the results from each partition to illustrate aggregation.
        //    // note that this can be optimized in multiple ways for production code.
        //    string serviceUri = this.serviceContext.CodePackageActivationContext.ApplicationName + "/" + this.configSettings.StatefulBackendServiceName;
        //    ServicePartitionList partitions = await this.fabricClient.QueryManager.GetPartitionListAsync(new Uri(serviceUri));

        //    List<KeyValuePair<string, string>> result = new List<KeyValuePair<string, string>>();

        //    JsonSerializer serializer = new JsonSerializer();
        //    foreach (Partition partition in partitions)
        //    {
        //        long partitionKey = ((Int64RangePartitionInformation) partition.PartitionInformation).LowKey;

        //        string proxyUrl =
        //            $"http://localhost:{this.configSettings.ReverseProxyPort}/{serviceUri.Replace("fabric:/", "")}/api/values?PartitionKind={partition.PartitionInformation.Kind}&PartitionKey={partitionKey}";

        //        HttpResponseMessage response = await this.httpClient.GetAsync(proxyUrl);

        //        if (response.StatusCode != System.Net.HttpStatusCode.OK)
        //        {
        //            // if one partition returns a failure, you can either fail the entire request or skip that partition.
        //            return this.StatusCode((int) response.StatusCode);
        //        }

        //        List<KeyValuePair<string, string>> list =
        //            JsonConvert.DeserializeObject<List<KeyValuePair<string, string>>>(await response.Content.ReadAsStringAsync());

        //        if (list != null && list.Any())
        //        {
        //            result.AddRange(list);
        //        }
        //    }

        //    return this.Json(result);
        //}

      
        [HttpPost]
        [SwaggerOperation("PostAsync")]
        public async Task<IActionResult> PostAsync([FromBody] Device device)
        {
            var serviceUri = serviceContext.CodePackageActivationContext.ApplicationName.Replace("fabric:/", "") + "/" +
                             configSettings.SoftwareUpdateServiceName;


            var putContent = new StringContent(device.DeviceId, Encoding.UTF8, "application/json");
            putContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            try
            {
                var response = await httpClient.PostAsJsonAsync(serviceUri, device.DeviceId);

                return new ContentResult
                {
                    StatusCode = (int) response.StatusCode,
                    Content = await response.Content.ReadAsStringAsync()
                };
            }
            catch (HttpRequestException e)
            {
                return new ContentResult
                {
                    StatusCode = e.HResult,
                    Content = e.Message
                };
            }
        }


        // GET api/values/5
        [HttpGet("{id}")]
        public string Get(int id)
        {
            throw new NotImplementedException(
                "No method implemented to get a specific key/value pair from the Stateful Backend Service");
        }


        // DELETE api/values/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
            throw new NotImplementedException(
                "No method implemented to delete a specified key/value pair in the Stateful Backend Service");
        }

/*
        private static int GetPartitionKey(string key)
        {
            // The partitioning scheme of the processing service is a range of integers from 0 - 25.
            // This generates a partition key within that range by converting the first letter of the input name
            // into its numerica position in the alphabet.
            var firstLetterOfKey = key.First();
            var partitionKeyInt = char.ToUpper(firstLetterOfKey) - 'A';

            if (partitionKeyInt < 0 || partitionKeyInt > 25)
                throw new ArgumentException("The key must begin with a letter between A and Z");

            return partitionKeyInt;
        }
*/
    }
}