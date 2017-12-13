namespace NServiceBus.Gateway.Channels.HttpVNext
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using System.Web;
    using Logging;

    [ChannelType("httpVNext")]
    [ChannelType("httpsVNext")]
    public class HttpVNextChannelSender : IChannelSender
    {
        public async Task Send(string remoteUrl, IDictionary<string, string> headers, Stream data)
        {
#pragma warning disable DE0003 // API is deprecated
            var request = WebRequest.Create(remoteUrl);
#pragma warning restore DE0003 // API is deprecated
            request.Method = "POST";
            request.ContentType = "application/json";
            request.UseDefaultCredentials = true;

            var payload = new Payload
            {
                Headers = headers,
                Message = data.ToByteArray()
            };

            var content = Encoding.UTF8.GetBytes(SimpleJson.SerializeObject(payload));
            request.ContentLength = content.Length;
            byte[] hash;
            using (var md5 = MD5.Create())
            {
                hash = md5.ComputeHash(content);
            }
            
            using (var stream = request.GetRequestStream())
            {
                stream.Write(content, 0, content.Length);
            }

            HttpStatusCode statusCode;
            var md5Ok = false;
            
            using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
            {
                statusCode = response.StatusCode;
                var responseContent = response.GetResponseStream().ToByteArray();
                var contentString = Encoding.UTF8.GetString(responseContent);
                md5Ok = (hash.ToHex() == contentString);
            }

            Logger.Debug("Got HTTP response with status code " + statusCode);

            if (statusCode != HttpStatusCode.OK || !md5Ok)
            {
                Logger.Warn("Message not transferred successfully. Trying again...");
                throw new Exception("Retrying");
            }
        }

        static readonly ILog Logger = LogManager.GetLogger<HttpVNextChannelSender>();
    }
}

