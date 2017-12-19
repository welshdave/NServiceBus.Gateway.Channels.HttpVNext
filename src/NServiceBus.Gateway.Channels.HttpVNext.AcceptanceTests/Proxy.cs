namespace NServiceBus.Gateway.Channels.HttpVNext.AcceptanceTests
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    public class Proxy
    {
        public void Start(string address, string addressToProxy)
        {
            siteToProxy = new Uri(addressToProxy);

            cancellationTokenSource = new CancellationTokenSource();
            cancellationToken = cancellationTokenSource.Token;

            listener = new HttpListener();

            listener.Prefixes.Add(address);

            try
            {
                listener.Start();
            }
            catch (Exception ex)
            {
                var message = $"Failed to start listener for {address} make sure that you have admin privileges";
                throw new Exception(message, ex);
            }

            Task.Run(ProcessRequests, CancellationToken.None);
        }

        public async Task Stop()
        {
            cancellationTokenSource?.Cancel();
            listener?.Close();


        }

        async Task ProcessRequests()
        {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync().ConfigureAwait(false);

                if (cancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }

                await ProxyRequest(context, cancellationToken);
            }
        }

        async Task ProxyRequest(HttpListenerContext context, CancellationToken token)
        {
            var client = new HttpClient();
            try
            {
                var localPath = context.Request.Url.LocalPath;
                var newRequest = new HttpRequestMessage
                {
                    RequestUri = new Uri($"{siteToProxy}{localPath}"),
                    Method = new HttpMethod(context.Request.HttpMethod),
                    Content = new StreamContent(context.Request.InputStream)
                };

                foreach (var header in context.Request.Headers.AllKeys)
                {
                    if (!header.Contains("."))
                    {
                        newRequest.Headers.TryAddWithoutValidation(header, context.Request.Headers[header]);
                    }
                }

                using (var response = await client.SendAsync(newRequest, HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken))
                {
                    context.Response.StatusCode = (int)response.StatusCode;
                    context.Response.ContentType = response.Content?.Headers.ContentType?.MediaType;

                    foreach (var header in response.Headers)
                    {
                        context.Response.Headers[header.Key] = string.Join(",", header.Value);
                    }

                    context.Response.Headers.Remove("transfer-encoding");

                    if (response.Content != null)
                    {
                        foreach (var contentHeader in response.Content.Headers)
                        {
                            context.Response.Headers[contentHeader.Key] = string.Join(",", contentHeader.Value);
                        }
                        context.Response.Close(await response.Content.ReadAsByteArrayAsync(), false);
                    }
                }
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            }
        }

        Uri siteToProxy;
        SemaphoreSlim concurrencyLimiter;
        HttpListener listener;
        CancellationTokenSource cancellationTokenSource;
        CancellationToken cancellationToken;
    }
}
