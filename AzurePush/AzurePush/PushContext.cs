using System;
using AzureStorageExtensions;

namespace AzurePush
{
    public class PushContext : BaseCloudContext
    {
        public CloudTable<NotiSubscription> NotiSubscriptions { get; set; }
        public CloudTable<NotiUserSubscription> NotiUserSubscriptions { get; set; } 
    }
}
