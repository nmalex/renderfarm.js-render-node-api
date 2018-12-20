using System;
using Newtonsoft.Json;

namespace WorkerManager
{
    public interface IWorker : IDisposable
    {
        event EventHandler Restarted;
        [JsonProperty(PropertyName = "pid")]
        int? Pid { get; }
        [JsonProperty(PropertyName = "port")]
        int Port { get; }
        [JsonIgnore]
        string Label { get; }

        void Start();
        void Kill();
        string GetRamUsage();
        string GetCpuLoad();
    }
}