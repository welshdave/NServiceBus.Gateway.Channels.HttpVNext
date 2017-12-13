namespace NServiceBus.Gateway.Channels.HttpVNext
{
    using System.Text;

    internal static class ByteExtensions
    {
        internal static string ToHex(this byte[] bytes)
        {
            var result = new StringBuilder(bytes.Length * 2);

            foreach (var t in bytes)
                result.Append(t.ToString("x2"));

            return result.ToString();
        }
    }
}
