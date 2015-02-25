using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Common.Logging;
using Newtonsoft.Json.Linq;
using PushSharp;
using PushSharp.Android;
using PushSharp.Apple;
using PushSharp.Core;

namespace AzurePush
{
    public class NotiPusher : IDisposable
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof (NotiPusher));

        public bool HasIos { get; set; }
        public bool HasAndriod { get; set; }

        private readonly PushBroker pushBroker;
        public NotiPusher(NotiConfig config)
        {
            pushBroker = new PushBroker();
            pushBroker.OnChannelException += OnChannelException;
            pushBroker.OnDeviceSubscriptionChanged += OnDeviceSubscriptionChanged;
            pushBroker.OnDeviceSubscriptionExpired += OnDeviceSubscriptionExpired;
            pushBroker.OnNotificationFailed += OnNotificationFailed;
            pushBroker.OnServiceException += OnServiceException;

            ConfigureIos(config);
            ConfigureAndriod(config);
        }

        void ConfigureIos(NotiConfig config)
        {
            this.HasIos = config.HasIos;
            if (this.HasIos)
            {
                try
                {
                    var bytes = File.ReadAllBytes(config.ApnCertFile);
                    var setting = new ApplePushChannelSettings(config.ApnProduction, bytes, config.ApnCertPassword);
                    pushBroker.RegisterAppleService(setting);
                }
                catch (Exception ex)
                {
                    logger.Error("Error when adding iOS", ex);

                    this.HasIos = false;
                }
            }
        }

        void ConfigureAndriod(NotiConfig config)
        {
            this.HasAndriod = config.HasAndroid;
            if (this.HasAndriod)
            {
                var setting = new GcmPushChannelSettings(config.GcmSenderId, config.GcmAuthToken, config.GcmPackageName);
                pushBroker.RegisterGcmService(setting);
            }
        }

        static void OnServiceException(object sender, Exception error)
        {
            logger.Error("ServiceException", error);
        }

        static void OnNotificationFailed(object sender, INotification notification, Exception error)
        {
            logger.Error(format => format("Notification failed: {0}", notification), error);
        }

        void OnDeviceSubscriptionExpired(object sender, string expiredSubscriptionId, DateTime expirationDateUtc, INotification notification)
        {
            try
            {
                RemoveSubscription(expiredSubscriptionId);
            }
            catch (Exception ex)
            {
                logger.Error("DeviceSubscriptionExpired", ex);
            }
        }

        void OnDeviceSubscriptionChanged(object sender, string oldSubscriptionId, string newSubscriptionId, INotification notification)
        {
            try
            {
                ChangeSubscription(oldSubscriptionId, newSubscriptionId);
            }
            catch (Exception ex)
            {
                logger.Error("DeviceSubscriptionChanged", ex);
            }
        }

        static void OnChannelException(object sender, IPushChannel pushChannel, Exception error)
        {
            logger.Error("ChannelException", error);
        }

        public void AddSubscription(string userId, string platform, string subscriptionId)
        {
            //check if subscription already exists
            var context = new PushContext();
            var nosign = subscriptionId.RemoveSign();
            var subscription = (from item in context.NotiSubscriptions.Query()
                                where item.PartitionKey == nosign &&
                                      item.Token == subscriptionId
                                select item).FirstOrDefault();
            if (subscription != null)
            {
                if (subscription.UserId == userId)
                    return;

                context.NotiUserSubscriptions.SafeDelete(userId.PrependHash(), nosign);
            }
            
            //insert subscription
            subscription = new NotiSubscription
            {
                PartitionKey = nosign,
                RowKey = Guid.NewGuid().ToString("N"),
                Token = subscriptionId,
                UserId = userId,
                Platform = platform,
            };
            context.NotiSubscriptions.Insert(subscription, true);
            var userSubscription = new NotiUserSubscription
            {
                PartitionKey = userId.PrependHash(),
                RowKey = nosign,
                Platform = platform,
                UserId = userId,
                Token = subscriptionId,
            };
            context.NotiUserSubscriptions.Insert(userSubscription, true);
        }

        public NotiSubscription RemoveSubscription(string subscriptionId)
        {
            var context = new PushContext();
            var nosign = subscriptionId.RemoveSign();
            var subscription = (from item in context.NotiSubscriptions.Query()
                                where item.PartitionKey == nosign &&
                                      item.Token == subscriptionId
                                select item).FirstOrDefault();
            if (subscription == null)
                return null;

            context.NotiUserSubscriptions.SafeDelete(subscription.UserId.PrependHash(), nosign);
            context.NotiSubscriptions.SafeDelete(subscription);
            return subscription;
        }

        public void ChangeSubscription(string oldSubscriptionId, string newSubscriptionId)
        {
            var oldSubscription = RemoveSubscription(oldSubscriptionId);
            if (oldSubscription != null)
                AddSubscription(oldSubscription.UserId, oldSubscription.Platform, newSubscriptionId);
        }

        public void Push(string userId, string objectId, string type, string message, int? badge = null)
        {
            var context = new PushContext();
            var hashed = userId.PrependHash();
            var pushes = (from item in context.NotiUserSubscriptions.Query()
                          where item.UserId == hashed
                          select item).ToLookup(item => item.Platform);

            Push(pushes, objectId, type, message, badge);
        }

        public void Push(int start, int end, string objectId, string type, string message)
        {
            var startHash = start.ToString("00000") + "|";
            var endHash = end.ToString("00000") + "||";

            var context = new PushContext();
            var pushes = (from item in context.NotiUserSubscriptions.Query()
                          where item.UserId.CompareTo(startHash) > 0 &&
                                item.UserId.CompareTo(endHash) < 0
                          select item).ToLookup(item => item.Platform);
            Push(pushes, objectId, type, message, null);
        }

        private void Push(ILookup<string, NotiUserSubscription> pushes, string objectId, string type, string message, int? badge)
        {
            if (this.HasIos && pushes["ios"].Any())
            {
                PushIos(pushes["ios"], objectId, type, message, badge);
            }

            if (this.HasAndriod && pushes["andriod"].Any())
            {
                PushAndroid(pushes["andriod"], objectId, type, message, badge);
            }
        }

        private void PushIos(IEnumerable<NotiUserSubscription> pushes, string objectId, string type, string message, int? badge)
        {
            //cut length based on utf
            //ios can get only 256 bytes total (2k for ios8)
            var oldLen = message.Length;
            message = message.Utf8Substring(150);
            if (message.Length != oldLen)
                message += "...";

            foreach (var push in pushes)
            {
                var noti = new AppleNotification
                {
                    DeviceToken = push.Token,
                };
                noti.Payload.Alert.Body = message;
                noti.Payload.Badge = badge;
                noti.Payload.AddCustom("objectId", objectId);
                noti.Payload.AddCustom("type", type);
                noti.Payload.AddCustom("time", DateTime.UtcNow.ToString("s"));
                pushBroker.QueueNotification(noti);
            }
        }

        private void PushAndroid(IEnumerable<NotiUserSubscription> pushes, string objectId, string type, string message, int? badge)
        {
            //cut length based on utf
            //gcm can get 4k total
            var oldLen = message.Length;
            message = message.Utf8Substring(4000);
            if (message.Length != oldLen)
                message += "...";

            var json = new JObject
                       {
                           { "alert", message },
                           { "objectId", objectId },
                           { "type", type },
                           { "time", DateTime.UtcNow.ToString("s") },
                       };
            if (badge.HasValue)
                json.Add("badge", badge);
            var tokens = pushes.Select(item => item.Token).ToList();
            var noti = new GcmNotification
            {
                RegistrationIds = tokens,
                CollapseKey = type,
                JsonData = json.ToString(),
            };
            pushBroker.QueueNotification(noti);
        }

        public void Dispose()
        {
            pushBroker.StopAllServices();
        }
    }
}
