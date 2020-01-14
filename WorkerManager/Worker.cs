using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using Timer = System.Threading.Timer;

namespace WorkerManager
{
    public class Worker : IWorker
    {
        private PerformanceCounter cpuCounter;
        private PerformanceCounter ramCounter;

        private readonly object cpuCounterLock = new object();
        private readonly object ramCounterLock = new object();
        private readonly object vrayProgressLock = new object();

        private string maxDirectory;
        private string maxExe;
        private TimeSpan unresponsiveTimeout;
        private IPAddress controllerHost;
        private int controllerPort;

        private int restartCount;
        private Timer timerProcessCheck;
        private DateTime? processUnresponsiveSince;
        private VrayRenderProgressSniffer renderingProgressSniffer;
        private readonly Settings settings;

        private static readonly Random Random = new Random(DateTime.Now.Millisecond);

        public event EventHandler Restarted;
        public event EventHandler<string> ProgressChanged;

        public Worker(string ip, int port, Settings settings)
        {
            this.Ip = ip;
            this.Port = port;
            this.settings = settings;
        }

        #region Properties

        private string Ip { get; }

        public int? Pid => this.Process?.Id;

        public int Port { get; }

        private string vrayProgress;
        public string VrayProgress
        {
            get
            {
                lock (this.vrayProgressLock)
                {
                    return this.vrayProgress;
                }
            }
            private set
            {
                lock (this.vrayProgressLock)
                {
                    this.vrayProgress = value;
                }
            }
        }

        public string Label => $"{this.Ip}:{this.Port} ({this.restartCount}) {(this.renderingProgressSniffer != null ? this.renderingProgressSniffer.ProgressText : "")}";

        private Process Process { get; set; }

        private PerformanceCounter CpuCounter
        {
            get
            {
                if (this.cpuCounter == null && this.Process != null)
                {
                    lock (this.cpuCounterLock)
                    {
                        if (this.cpuCounter == null && this.Process != null)
                        {
                            this.cpuCounter = new PerformanceCounter
                            {
                                CategoryName = "Process",
                                CounterName = "% Processor Time",
                                InstanceName = GetInstanceNameForProcessId(this.Process.Id)
                            };
                        }
                    }
                }
                return this.cpuCounter;
            }
        }

        private PerformanceCounter RamCounter
        {
            get
            {
                if (this.ramCounter == null && this.Process != null)
                {
                    lock (this.ramCounterLock)
                    {
                        if (this.ramCounter == null && this.Process != null)
                        {
                            this.ramCounter = new PerformanceCounter
                            {
                                CategoryName = "Process",
                                CounterName = "Working Set - Private",
                                InstanceName = GetInstanceNameForProcessId(this.Process.Id)
                            };
                        }
                    }
                }
                return this.ramCounter;
            }
        }

        #endregion

        public string GetRamUsage()
        {
            lock (this.ramCounterLock)
            {
                return this.RamCounter?.NextValue().ToString(CultureInfo.InvariantCulture);
            }
        }

        public string GetCpuLoad()
        {
            lock (this.cpuCounterLock)
            {
                return this.CpuCounter.NextValue().ToString(CultureInfo.InvariantCulture);
            }
        }

        public void BringToFront()
        {
            if (this.Process != null)
            {
                EnumerateOpenedWindows.SetForegroundWindow(this.Process.MainWindowHandle);
            }
        }

        public void Start()
        {
            var t = new Thread(() =>
            {
                Thread.Sleep((int)(15 * 1000 * Random.NextDouble()));
                this.Start(false);
            });
            t.Start();
        }

        private void Restart()
        {
            var t = new Thread(() =>
            {
                Thread.Sleep((int)(15 * 1000 * Random.NextDouble()));
                this.Start(true);
            });
            t.Start();
        }

        private void Start(bool isRestart)
        {
            this.maxDirectory = (string)this.settings["3dsmax_dir"];
            this.maxExe = (string)this.settings["3dsmax_exe"];

            this.unresponsiveTimeout = TimeSpan.FromSeconds((long)this.settings["unresponsive_timeout"]);

            var controllerHostValue = (string)this.settings["controller_host"];

            if (!IPAddress.TryParse(controllerHostValue, out this.controllerHost))
            {
                var hostEntry = Dns.GetHostEntry(controllerHostValue);
                this.controllerHost = hostEntry.AddressList.FirstOrDefault();
                if (this.controllerHost == null)
                {
                    MessageBox.Show("Error", "Can't resolve DNS name: " + controllerHostValue, MessageBoxButtons.OK);
                    Application.Exit();
                }
            }

            this.controllerPort = (int)this.settings["controller_port"];

            //prepare startup.ms file for this worker
            var workgroupValue = (string)this.settings["workgroup"];
            var startupScriptFilename = Path.Combine(Path.GetTempPath(), $"worker_{this.Port}.ms");
            File.WriteAllText(startupScriptFilename, $"threejsApiStart {this.Port} \"{this.controllerHost}\" {this.controllerPort} \"{workgroupValue}\"");

            //start worker process with parameters
            //learn more about command line parameters here: 
            //https://knowledge.autodesk.com/support/3ds-max/learn-explore/caas/CloudHelp/cloudhelp/2019/ENU/3DSMax-Basics/files/GUID-1A97CFEC-60A3-4221-B9C3-5C808E2AED35-htm.html
            var startInfo = new ProcessStartInfo(this.maxExe, $"-ma -dfc -silent -vxs -U MAXScript {startupScriptFilename}")
            {
                WorkingDirectory = this.maxDirectory,
                WindowStyle = ProcessWindowStyle.Minimized,
            };
            this.Process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true, 
            };

            //update UI
            if (this.Process != null)
            {
                this.Process.Exited += OnWorkerStopped;

                this.Process.Start();
                this.Process.PriorityClass = ProcessPriorityClass.Idle;

                this.timerProcessCheck = new Timer(OnProcessCheckTimer, null, TimeSpan.FromMilliseconds(1000), TimeSpan.FromSeconds(5));
                this.renderingProgressSniffer = new VrayRenderProgressSniffer(this.Process);
                this.renderingProgressSniffer.ProgressChanged += OnRenderProgressChanged;

                if (isRestart)
                {
                    this.restartCount ++;
                    this.Restarted?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        //todo: Process check is SRP violation, Worker is responsible for restart
        private void OnProcessCheckTimer(object state)
        {
            try
            {
                if (this.Process == null || this.Process.HasExited)
                {
                    return;
                }

                if (this.Process.Responding)
                {
                    processUnresponsiveSince = null;
                }
                else
                {
                    if (processUnresponsiveSince == null)
                    {
                        processUnresponsiveSince = DateTime.Now;
                    }

                    var unresponsiveTime = DateTime.Now.Subtract(this.processUnresponsiveSince.Value);
                    if (unresponsiveTime > this.unresponsiveTimeout)
                    {
                        //kill all processes that hang longer than unresponsiveTimeout
                        this.Process.Kill();

                        this.renderingProgressSniffer.ProgressChanged -= OnRenderProgressChanged;
                        this.renderingProgressSniffer.Dispose();
                        this.renderingProgressSniffer = null;
                    }
                }
            }
            catch (Exception)
            {
                // whatever happens here, we don't care
            }
        }

        public void Kill()
        {
            if (this.Process != null)
            {
                if (this.renderingProgressSniffer != null)
                {
                    this.renderingProgressSniffer.ProgressChanged -= OnRenderProgressChanged;
                    this.renderingProgressSniffer?.Dispose();
                    this.renderingProgressSniffer = null;
                }

                this.timerProcessCheck?.Dispose();
                this.timerProcessCheck = null;

                this.Process.Exited -= OnWorkerStopped;

                if (!this.Process.HasExited)
                {
                    this.Process.Kill();
                }

                this.Process = null;
            }
        }

        public void Dispose()
        {
            if (this.renderingProgressSniffer != null)
            {
                this.renderingProgressSniffer.ProgressChanged -= OnRenderProgressChanged;
                this.renderingProgressSniffer.Dispose();
                this.renderingProgressSniffer = null;
            }

            // dispose cpu perf counter
            lock (this.cpuCounterLock)
            {
                if (this.cpuCounter != null)
                {
                    this.cpuCounter.Close();
                    this.cpuCounter.Dispose();
                    this.cpuCounter = null;
                }
            }

            // dispose ram perf counter
            lock (this.ramCounterLock)
            {
                if (this.ramCounter != null)
                {
                    this.ramCounter.Close();
                    this.ramCounter.Dispose();
                    this.ramCounter = null;
                }
            }
        }

        private void OnWorkerStopped(object sender, EventArgs eventArgs)
        {
            this.renderingProgressSniffer.ProgressChanged -= OnRenderProgressChanged;
            this.renderingProgressSniffer?.Dispose();
            this.renderingProgressSniffer = null;

            this.timerProcessCheck?.Dispose();
            this.timerProcessCheck = null;

            var process = (Process)sender;
            process.Exited -= OnWorkerStopped;
            process.Dispose();

            this.Restart();
        }

        private void OnRenderProgressChanged(object sender, string progressText)
        {
            this.VrayProgress = progressText;
            this.ProgressChanged?.Invoke(this, progressText);
        }

        private static string GetInstanceNameForProcessId(int processId)
        {
            var process = Process.GetProcessById(processId);
            var processName = Path.GetFileNameWithoutExtension(process.ProcessName);

            var cat = new PerformanceCounterCategory("Process");
            var instances = cat.GetInstanceNames()
                .Where(inst => inst.StartsWith(processName))
                .ToArray();

            foreach (var instance in instances)
            {
                using (var cnt = new PerformanceCounter("Process", "ID Process", instance, true))
                {
                    var val = (int)cnt.RawValue;
                    if (val == processId)
                    {
                        return instance;
                    }
                }
            }

            return null;
        }
    }
}