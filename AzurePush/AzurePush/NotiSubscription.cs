using Microsoft.WindowsAzure.Storage.Table;

namespace AzurePush
{
    public class NotiSubscription : TableEntity
    {
        public string UserId { get; set; }
        public string Token { get; set; }
        public string Platform { get; set; }
    }
}
