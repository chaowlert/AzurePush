namespace AzurePush
{
    public class NotiConfig
    {
        public bool HasIos { get; set; }
        public string ApnCertFile { get; set; }
        public string ApnCertPassword { get; set; }
        public bool ApnProduction { get; set; }

        public bool HasAndroid { get; set; }
        public string GcmSenderId { get; set; }
        public string GcmAuthToken { get; set; }
        public string GcmPackageName { get; set; }
    }
}