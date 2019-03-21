using System;
using System.Threading;
using System.Threading.Tasks;
using durable_functions_service.Constants;
using durable_functions_service.Enums;
using durable_functions_service.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace durable_functions_service
{
    public static class MessageProcessingOrchestrator
    {
        [FunctionName(Orchestrators.ProcessMessage)]
        public static async Task ProcessMessage([OrchestrationTrigger] DurableOrchestrationContext context, ILogger log)
        {
            var trigger = context.GetInput<MessageTrigger>();
            var approvalResult = ApprovalResult.Unknown;
            string emailAddress = null;

            try
            {
                log.LogWarning("Getting email address from database.");
                emailAddress = await context.CallActivityAsync<string>(Activities.GetEmail, trigger.Id);

                log.LogWarning($"Sending email to: {emailAddress}");
                await context.CallActivityAsync(Activities.SendEmail,
                    new SendEmailData {Email = emailAddress, Id = context.InstanceId});

                using (var cts = new CancellationTokenSource())
                {
                    var timeout = context.CurrentUtcDateTime.AddMinutes(5);
                    var timeoutTask = context.CreateTimer(timeout, cts.Token);
                    var approvalTask = context.WaitForExternalEvent<string>(Events.ApprovalResult);

                    log.LogWarning("Waiting for user action");
                    var finishedTask = await Task.WhenAny(timeoutTask, approvalTask);

                    if (finishedTask == approvalTask)
                    {
                        approvalResult = approvalTask.Result == "Approved"
                            ? ApprovalResult.Approved
                            : ApprovalResult.Rejected;
                        cts.Cancel();
                    }
                    else
                    {
                        approvalResult = ApprovalResult.Rejected;
                    }
                }
            }
            catch (Exception ex)
            {
                log.LogError($"Exception thrown while processing message: {ex.Message}");

                await context.CallActivityAsync(Activities.PostResultOnQueue,
                    new ApprovalResultOutput {Email = emailAddress, Id = trigger.Id.ToString(), Result = ApprovalResult.Unknown});
            }

            log.LogWarning("Posting result message");
            await context.CallActivityAsync(Activities.PostResultOnQueue,
                new ApprovalResultOutput {Email = emailAddress, Id = trigger.Id.ToString(), Result = approvalResult});
        }
    }
}
