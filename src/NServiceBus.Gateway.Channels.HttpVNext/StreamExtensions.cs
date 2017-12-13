namespace NServiceBus.Gateway.Channels.HttpVNext
{
    using System.IO;

    internal static class StreamExtensions
    {
        internal static byte[] ToByteArray(this Stream stream)
        {
            if (stream.GetType() == typeof(MemoryStream))
            {
                return ((MemoryStream) stream).ToArray();
            }
            var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }
    }
}
