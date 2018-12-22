using System;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.Net.Http.Server;
using Newtonsoft.Json;

namespace WorkerManager
{
    public class WorkerManagerEndpoint
    {
        private readonly IWorkersManager workersManager;
        private bool threadRunning;
        private Thread thread;
        private string host;
        private int port;
        private WebListener listener;

        public WorkerManagerEndpoint(IWorkersManager workersManager)
        {
            this.workersManager = workersManager;
        }

        public void Listen(string ahost, int aport)
        {
            this.host = ahost;
            this.port = aport;

            this.threadRunning = true;
            this.thread = new Thread(ListenThread);
            this.thread.Start(null);
        }

        public void Close()
        {
            if (this.thread == null)
            {
                return;
            }

            this.threadRunning = false;
            this.listener.Dispose();
            this.thread.Join();
        }

        private void ListenThread(object obj)
        {
            var settings = new WebListenerSettings();
            settings.UrlPrefixes.Add($"http://{this.host}:{this.port}");

            this.listener = new WebListener(settings);
            this.listener.Start();

            while (this.threadRunning)
            {
                var task = listener.AcceptAsync();

                task.ContinueWith(o =>
                {
                    if (o.IsFaulted)
                    {
                        return;
                    }

                    var context = o.Result;
                    if (context.Request.Method == "GET")
                    {
                        HandleGetRequest(context);
                    }
                    if (context.Request.Method == "POST")
                    {
                        HandlePostRequest(context);
                    }
                    if (context.Request.Method == "PUT")
                    {
                        HandlePutRequest(context);
                    }
                    if (context.Request.Method == "OPTIONS")
                    {
                        HandleOptionsRequest(context);
                    }
                    if (context.Request.Method == "DELETE")
                    {
                        HandleDeleteRequest(context);
                    }

                }).Wait();
            }
        }

        private void HandleGetRequest(RequestContext context)
        {
            var parts = context.Request.Path.Split(new[] { "/" }, StringSplitOptions.RemoveEmptyEntries);

            var resource = parts[0];
            if (resource == "worker")
            {
                var serializer = new JsonSerializer();
                var sb = new StringBuilder();
                serializer.Serialize(new StringWriter(sb), this.workersManager.Workers);

                WriteHeaders(context);
                WriteResponse(context, sb.ToString());
            }
        }

        private void HandlePostRequest(RequestContext context)
        {
            var parts = context.Request.Path.Split(new[] { "/" }, StringSplitOptions.RemoveEmptyEntries);

            var resource = parts[0];
            if (resource == "worker")
            {
                var worker = this.workersManager.AddWorker();

                WriteHeaders(context);
                WriteResponse(context, $"{{\"success\": true, \"port\": {worker.Port}}}");
            }
        }

        private void HandlePutRequest(RequestContext context)
        {
            var parts = context.Request.Path.Split(new[] { "/" }, StringSplitOptions.RemoveEmptyEntries);

            var resource = parts[0];
            if (resource == "worker")
            {
                var s = new StreamReader(context.Request.Body);
                var body = s.ReadToEnd();

                var a = new JsonSerializer();
                var c = a.Deserialize<WorkerInfo>(new JsonTextReader(new StringReader(body)));

                WriteHeaders(context);
                WriteResponse(context, $"{{\"error\": \"not implemented\", \"port\": {c.Port}}}");
            }
        }

        private void HandleOptionsRequest(RequestContext context)
        {
            var parts = context.Request.Path.Split(new[] { "/" }, StringSplitOptions.RemoveEmptyEntries);

            var resource = parts[0];
            if (resource == "worker")
            {
                var bytes = Encoding.ASCII.GetBytes(string.Empty);
                context.Response.ContentLength = bytes.Length;
                WriteHeaders(context);

                context.Response.Body.WriteAsync(bytes, 0, bytes.Length).ContinueWith(p =>
                {
                    context.Dispose();
                });
            }
        }

        private void HandleDeleteRequest(RequestContext context)
        {
            var parts = context.Request.Path.Split(new[] { "/" }, StringSplitOptions.RemoveEmptyEntries);

            var resource = parts[0];
            if (resource == "worker")
            {
                HandleDeleteWorker(context);
            }
        }

        private void HandleDeleteWorker(RequestContext context)
        {
            var parts = context.Request.Path.Split(new[] { "/" }, StringSplitOptions.RemoveEmptyEntries);

            int workerPort;
            if (!int.TryParse(parts[1], out workerPort))
            {
                WriteResponse(context, "{\"error\": \"wrong worker port\"}");
                return;
            }

            if (!this.workersManager.KillWorker(workerPort))
            {
                WriteResponse(context, "{\"error\": \"worker not found\"}");
                return;
            }

            var response = $"{{\"success\": true, \"port\": {workerPort}}}";
            WriteResponse(context, response);
        }

        private static void WriteResponse(RequestContext context, string response)
        {
            var bytes = Encoding.ASCII.GetBytes(response);
            context.Response.ContentLength = bytes.Length;
            WriteHeaders(context);

            context.Response.Body.WriteAsync(bytes, 0, bytes.Length).ContinueWith(p => { context.Dispose(); });
        }

        private static void WriteHeaders(RequestContext context)
        {
            context.Response.Headers["Access-Control-Allow-Origin"] = "*";
            context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, OPTIONS";
            context.Response.ContentType = "application/json";
        }
    }

    public class WorkerInfo
    {
        [JsonProperty(PropertyName = "port")]
        public int Port { get; set; }
    }
}