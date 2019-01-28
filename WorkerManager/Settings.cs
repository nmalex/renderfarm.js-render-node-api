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
        private readonly JsonSerializer serializer;
        private JObject jobject;

        public Settings(string filename)
        {
            this.filename = filename;
            this.serializer = JsonSerializer.CreateDefault();
        }

        public string FilePath => this.filename;

        public JToken this[string i]
        {
            get
            {
                lock (this.sync)
                {
                    return this.jobject[i];
                }
            }
            set
            {
                lock (this.sync)
                {
                    this.jobject[i] = value;
                }
            }
        }

        public void Load()
        {
            lock (this.sync)
            {
                var jsonStr = File.ReadAllText(this.filename);
                this.jobject = (JObject)this.serializer.Deserialize(new JsonTextReader(new StringReader(jsonStr)));
            }
        }

        public void Save()
        {
            lock (this.sync)
            {
                var sb = new StringBuilder();
                JsonSerializer.CreateDefault().Serialize(new StringWriter(sb), jobject);
                var sbs = sb.ToString();
                File.WriteAllText(this.filename, sbs);
            }
        }
    }
}