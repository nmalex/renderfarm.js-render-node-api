using Newtonsoft.Json;

namespace WorkerManager
{
    public class WorkerInfo
    {
        [JsonProperty(PropertyName = "port")]
        public int Port { get; set; }
    }
}