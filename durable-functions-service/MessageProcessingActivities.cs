using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using durable_functions_service.Constants;
using durable_functions_service.Enums;
using durable_functions_service.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;
using SendGrid.Helpers.Mail;

namespace durable_functions_service
{
    public static class MessageProcessingActivities
    {
        private static string ConnectionString => Environment.GetEnvironmentVariable("ConnectionString");

        [FunctionName(Activities.SendEmail)]
        public static void SendEmail([ActivityTrigger] SendEmailData emailData,
            [SendGrid(ApiKey = "SendGridKey")] out SendGridMessage message)
        {
            var approverEmail = new EmailAddress(emailData.Email);
            var senderEmail = new EmailAddress(Environment.GetEnvironmentVariable("SenderEmail"));
            var subject = "A task is awaiting approval";

            var host = Environment.GetEnvironmentVariable("Host");
            var functionAddress = $"{host}/api/Approval/{emailData.Id}";
            var approvedLink = functionAddress + "?result=Approved";
            var rejectedLink = functionAddress + "?result=Rejected";
            var body = $"<a href=\"{approvedLink}\">Approve</a><br>"
                       + $"<a href=\"{rejectedLink}\">Reject</a>";

            message = MailHelper.CreateSingleEmail(senderEmail, approverEmail, subject, "", body);
        }

        [FunctionName(Activities.GetEmail)]
        public static async Task<string> GetEmail([ActivityTrigger] int id)
        {
            string email = null;

            using (var connection = new SqlConnection(ConnectionString))
            {
                await connection.OpenAsync();
                const string command = "SELECT Email FROM Emails WHERE Id=@ID";

                using (var cmd = new SqlCommand(command, connection))
                {
                    cmd.Parameters.AddWithValue("@ID", id);

                    var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);

                    if (reader.Read())
                    {
                        email = reader.GetString(0);
                    }
                }
            }

            return email;
        }

        [FunctionName(Activities.PostResultOnQueue)]
        public static async Task PostResultOnQueue([ActivityTrigger] ApprovalResultOutput result,
            [Queue("approval-result", Connection = "QueueConnectionString")]
            CloudQueue outputQueue)
        {
            var message = new
            {
                result.Id,
                Result = MapResult(result.Result)
            };

            await outputQueue.AddMessageAsync(new CloudQueueMessage(JsonConvert.SerializeObject(message)));
        }

        private static string MapResult(ApprovalResult result)
        {
            switch (result)
            {
                case ApprovalResult.Approved:
                {
                    return "Approved";
                }
                case ApprovalResult.Rejected:
                {
                    return "Rejected";
                }
                case ApprovalResult.Unknown:
                {
                    return "Unknown";
                }
                default:
                {
                    return "Unknown";
                }
            }
        }
    }
}
