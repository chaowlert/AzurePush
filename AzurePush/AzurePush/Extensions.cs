namespace AzurePush
{
    internal static class Extensions
    {
        public static string PrependHash(this string userId)
        {
            var i = userId.GetHashCode() % 100000;
            return i.ToString("00000") + "|" + userId;
        }
    }
}
