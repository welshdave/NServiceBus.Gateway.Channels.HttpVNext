namespace NServiceBus.Gateway.Channels.HttpVNext
{
    using System.Collections.Generic;

    public class Payload
    {
        public IDictionary<string, string> Headers { get; set; }

        public byte[] Message { get; set; }
    }
}
