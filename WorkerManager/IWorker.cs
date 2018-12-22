using System;
using Newtonsoft.Json;

namespace WorkerManager
{
    public interface IWorker : IDisposable
    {
        event EventHandler Restarted;
        event EventHandler<string> ProgressChanged;

        [JsonProperty(PropertyName = "pid")]
        int? Pid { get; }

        [JsonProperty(PropertyName = "port")]
        int Port { get; }

        [JsonProperty(PropertyName = "vray_progress")]
        string VrayProgress { get; }

        [JsonIgnore]
        string Label { get; }

        void Start();
        void Kill();
        string GetRamUsage();
        string GetCpuLoad();
        void BringToFront();
    }
}