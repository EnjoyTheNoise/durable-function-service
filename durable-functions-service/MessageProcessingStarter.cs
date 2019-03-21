using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using durable_functions_service.Constants;
using durable_functions_service.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace durable_functions_service
{
    public static class MessageProcessingStarter
    {
        [FunctionName("MessageProcessingStarter")]
        public static async Task Run([QueueTrigger("notify", Connection = "QueueConnectionString")]
            string message, [OrchestrationClient] DurableOrchestrationClient starter, ILogger log)
        {
            var messageData = JsonConvert.DeserializeObject<MessageTrigger>(message);

            log.LogWarning($"Starter function triggered by message: {message}");

            await starter.StartNewAsync(Orchestrators.ProcessMessage, messageData);
        }

        [FunctionName("Approval")]
        public static async Task<HttpResponseMessage> Approval(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Approval/{id}")]
            HttpRequestMessage req, [OrchestrationClient] DurableOrchestrationClient client, string id, ILogger log)
        {
            var result = req.RequestUri.ParseQueryString()["result"];
            await client.RaiseEventAsync(id, Events.ApprovalResult, result);

            log.LogWarning($"External event triggered by request: {req}");

            return req.CreateResponse(HttpStatusCode.OK);
        }
    }
}
