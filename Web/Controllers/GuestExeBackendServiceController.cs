#region

using System;
using System.Fabric;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

#endregion

namespace Web.Controllers
{
    [Route("api/[controller]")]
    public class GuestExeBackendServiceController : Controller
    {
        private readonly ConfigSettings configSettings;
        private readonly FabricClient fabricClient;
        private readonly HttpClient httpClient;
        private readonly StatelessServiceContext serviceContext;

        public GuestExeBackendServiceController(StatelessServiceContext serviceContext, HttpClient httpClient,
            FabricClient fabricClient, ConfigSettings settings)
        {
            this.serviceContext = serviceContext;
            this.httpClient = httpClient;
            configSettings = settings;
            this.fabricClient = fabricClient;
        }

        // GET: api/values
        [HttpGet]
        public async Task<IActionResult> GetAsync()
        {
            var serviceUri =
                $"{serviceContext.CodePackageActivationContext.ApplicationName}/{configSettings.GuestExeBackendServiceName}".
                    Replace("fabric:/", "");

            var proxyUrl = $"http://localhost:{configSettings.ReverseProxyPort}/{serviceUri}?cmd=instance";

            var response = await httpClient.GetAsync(proxyUrl);

            if (response.StatusCode != HttpStatusCode.OK) return StatusCode((int) response.StatusCode);

            return Ok(await response.Content.ReadAsStringAsync());
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
    }
}