using AzureStorageExtensions;
using Microsoft.WindowsAzure.Storage.Table;

namespace AzurePush
{
    internal static class Extensions
    {
        public static string PrependHash(this string userId)
        {
            var i = userId.GetHashCode() % 100000;
            return i.ToString("00000") + "|" + userId;
        }

        public static void SafeDelete<T>(this CloudTable<T> table, string partitionKey, string rowKey) where T : class, ITableEntity, new()
        {
            try
            {
                table.Delete(partitionKey, rowKey);
            }
            catch { }
        }

        public static void SafeDelete<T>(this CloudTable<T> table, T entity) where T : class, ITableEntity, new()
        {
            try
            {
                table.Delete(entity);
            }
            catch { }
        }
    }
}
