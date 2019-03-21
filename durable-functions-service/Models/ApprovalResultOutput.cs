using durable_functions_service.Enums;

namespace durable_functions_service.Models
{
    public class ApprovalResultOutput
    {
        public string Email { get; set; }
        public string Id { get; set; }
        public ApprovalResult Result { get; set; }
    }
}
