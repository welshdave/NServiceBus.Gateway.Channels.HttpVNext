﻿namespace NServiceBus.Gateway.Channels.HttpVNext
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;

    public class HttpVNextChannelReceiver : IChannelReceiver
    {
        public void Start(string address, int maxConcurrency, Func<DataReceivedOnChannelArgs, Task> dataReceivedOnChannel)
        {
            dataReceivedHandler = dataReceivedOnChannel;

            concurrencyLimiter = new SemaphoreSlim(maxConcurrency, maxConcurrency);

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

            messagePumpTask = Task.Run(ProcessMessages, CancellationToken.None);
        }

        public async Task Stop()
        {
            Logger.InfoFormat("Stopping channel - {0}", typeof(HttpVNextChannelReceiver));

            cancellationTokenSource?.Cancel();
            listener?.Close();

            // ReSharper disable once MethodSupportsCancellation
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
            var allTasks = runningReceiveTasks.Values.Concat(new[]
            {
                messagePumpTask
            });

            var finishedTask = await Task.WhenAny(Task.WhenAll(allTasks), timeoutTask).ConfigureAwait(false);

            if (finishedTask.Equals(timeoutTask))
            {
                Logger.Error("The http message pump failed to stop with in the time allowed(30s)");
            }

            concurrencyLimiter.Dispose();
            runningReceiveTasks.Clear();
        }

        async Task ProcessMessages()
        {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    var context = await listener.GetContextAsync().ConfigureAwait(false);

                    if (cancellationTokenSource.IsCancellationRequested)
                    {
                        return;
                    }

                    await concurrencyLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);

                    var receiveTask = HandleMessage(context, cancellationToken);

                    runningReceiveTasks.TryAdd(receiveTask, receiveTask);

                    receiveTask.ContinueWith(t =>
                    {
                        runningReceiveTasks.TryRemove(receiveTask, out Task _);
                    }, TaskContinuationOptions.ExecuteSynchronously)
                    .Forget();
                }
                catch (HttpListenerException ex)
                {
                    // a HttpListenerException can occur on listener.GetContext when we shutdown. this can be ignored
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Logger.Error("Gateway failed to receive incoming request.", ex);
                    }
                    break;
                }
                catch (ObjectDisposedException ex)
                {
                    // a ObjectDisposedException can occur on listener.GetContext when we shutdown. this can be ignored
                    if (!cancellationToken.IsCancellationRequested && ex.ObjectName == typeof(HttpListener).FullName)
                    {
                        Logger.Error("Gateway failed to receive incoming request.", ex);
                    }
                    break;
                }
                catch (InvalidOperationException ex)
                {
                    Logger.Error("Gateway failed to receive incoming request.", ex);
                    break;
                }
            }
        }

        async Task HandleMessage(HttpListenerContext context, CancellationToken token)
        {
            try
            {
                var payloadBytes = await GetPayloadBytes(context, token);
                var payload = GetPayload(payloadBytes);

                var dataStream = new MemoryStream(payload.Message);
                
                await dataReceivedHandler(new DataReceivedOnChannelArgs
                {
                    Headers = payload.Headers,
                    Data = dataStream
                }).ConfigureAwait(false);

                byte[] hash;
                using (var md5 = MD5.Create())
                {
                    hash = md5.ComputeHash(payloadBytes);
                }

                ReportSuccess(context, hash);

                Logger.Debug("Http request processing complete.");
            }
            catch (OperationCanceledException ex)
            {
                Logger.Info("Operation cancelled while shutting down the gateway", ex);
            }
            catch (Exception ex)
            {
                Logger.Error("Unexpected error", ex);
                CloseResponseAndWarn(context, "Unexpected server error", 502);
            }
            finally
            {
                concurrencyLimiter.Release();
            }
        }
        
        static Payload GetPayload(byte[] bytes)
        {
            var json = Encoding.UTF8.GetString(bytes);
            return SimpleJson.DeserializeObject<Payload>(json);
        }

        static async Task<byte[]> GetPayloadBytes(HttpListenerContext context, CancellationToken token)
        {
            var streamToReturn = new MemoryStream();

            await context.Request.InputStream.CopyToAsync(streamToReturn, MaximumBytesToRead, token).ConfigureAwait(false);
            streamToReturn.Position = 0;

            return streamToReturn.ToByteArray();
        }

        static void ReportSuccess(HttpListenerContext context, byte[] hash)
        {
            Logger.Debug("Sending HTTP 200 response.");

            context.Response.StatusCode = 200;
            context.Response.StatusDescription = "OK";

            WriteData(context, hash.ToHex());
        }

        static void WriteData(HttpListenerContext context, string content)
        {
            context.Response.AddHeader("Content-Type", "text/plain; charset=utf-8");
            
            context.Response.Close(Encoding.ASCII.GetBytes(content), false);
        }

        static void CloseResponseAndWarn(HttpListenerContext context, string warning, int statusCode)
        {
            try
            {
                Logger.WarnFormat("Cannot process HTTP request from {0}. Reason: {1}.", context.Request.RemoteEndPoint, warning);
                context.Response.StatusCode = statusCode;
                context.Response.StatusDescription = warning;

                WriteData(context, warning);
            }
            catch (Exception e)
            {
                Logger.Error("Could not return warning to client.", e);
            }
        }

        const int MaximumBytesToRead = 100000;

        static ILog Logger = LogManager.GetLogger<HttpVNextChannelReceiver>();
        SemaphoreSlim concurrencyLimiter;
        HttpListener listener;
        CancellationTokenSource cancellationTokenSource;
        CancellationToken cancellationToken;
        Task messagePumpTask;
        ConcurrentDictionary<Task, Task> runningReceiveTasks = new ConcurrentDictionary<Task, Task>();
        Func<DataReceivedOnChannelArgs, Task> dataReceivedHandler;
    }
}
