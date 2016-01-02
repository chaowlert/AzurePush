using System.Text;
using System.Text.RegularExpressions;

namespace AzurePush
{
    internal static class Extensions
    {
        public static string Utf8Substring(this string text, int byteLimit)
        {
            int byteCount = 0;
            char[] buffer = new char[1];
            for (int i = 0; i < text.Length; i++)
            {
                buffer[0] = text[i];
                byteCount += Encoding.UTF8.GetByteCount(buffer);
                if (byteCount > byteLimit)
                {
                    // Couldn't add this character. Return its index
                    return text.Substring(0, i);
                }
            }
            return text;
        }

        public static string RemoveSign(this string text)
        {
            return Regex.Replace(text, @"\W+", "");
        }

        public static string PrependHash(this string userId)
        {
            var i = userId.GetHashCode() % 100000;
            return i.ToString("00000") + "|" + userId;
        }
    }
}
