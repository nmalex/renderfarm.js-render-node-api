using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WorkerManager
{
    public class Settings
    {
        private readonly object sync = new object();
        private readonly string filename;
        private readonly Dictionary<string, JValue> values;
        private readonly JsonSerializer serializer;

        public Settings(string filename)
        {
            this.filename = filename;
            this.serializer = JsonSerializer.CreateDefault();
            this.values = new Dictionary<string, JValue>();
        }

        public JValue this[string i]
        {
            get
            {
                lock (this.sync)
                {
                    return values[i];
                }
            }
            set
            {
                lock (this.sync)
                {
                    values[i] = value;
                }
            }
        }

        public void Read()
        {
            lock (this.sync)
            {
                var jsonStr = File.ReadAllText(this.filename);
                var d = (JObject)this.serializer.Deserialize(new JsonTextReader(new StringReader(jsonStr)));
                this.values.Clear();
                foreach (var i in d)
                {
                    values[i.Key] = i.Value.Value<JValue>();
                }
            }
        }

        public void Save()
        {
            lock (this.sync)
            {
                var sb = new StringBuilder();
                JsonSerializer.CreateDefault().Serialize(new StringWriter(sb), values);
                var sbs = sb.ToString();
                File.WriteAllText(this.filename, sbs);
            }
        }
    }
}