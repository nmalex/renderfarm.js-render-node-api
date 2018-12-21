using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace WorkerManager
{
    public class Worker : IWorker
    {
        private PerformanceCounter cpuCounter;
        private PerformanceCounter ramCounter;

        private readonly object cpuCounterLock = new object();
        private readonly object ramCounterLock = new object();

        private readonly string controllerHost;
        private readonly string exeFile;
        private readonly string workDir;
        private readonly TimeSpan unresponsiveTimeout;

        private int restartCount;
        private Timer timerProcessCheck;
        private DateTime? processUnresponsiveSince;

        public event EventHandler Restarted;

        public Worker(string ip, int port, string controllerHost, string exeFile, string workDir, TimeSpan unresponsiveTimeout)
        {
            this.Ip = ip;
            this.Port = port;

            this.controllerHost = controllerHost;
            this.exeFile = exeFile;
            this.workDir = workDir;
            this.unresponsiveTimeout = unresponsiveTimeout;
        }

        #region Properties

        private string Ip { get; }

        public int? Pid => this.Process?.Id;

        public int Port { get; }

        public string Label => $"{this.Ip}:{this.Port} ({this.restartCount})";

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
            return this.RamCounter?.NextValue().ToString(CultureInfo.InvariantCulture);
        }

        public string GetCpuLoad()
        {
            return this.CpuCounter.NextValue().ToString(CultureInfo.InvariantCulture);
        }

        public void Start()
        {
            this.Start(false);
        }

        private void Restart()
        {
            this.Start(true);
        }

        private void Start(bool isRestart)
        {
            //prepare startup.ms file for this worker
            var startupScriptFilename = Path.Combine(Path.GetTempPath(), $"worker_{this.Port}.ms");
            File.WriteAllText(startupScriptFilename, $"threejsApiStart {this.Port} \"{this.controllerHost}\"");

            //start worker process with parameters
            //learn more about command line parameters here: 
            //https://knowledge.autodesk.com/support/3ds-max/learn-explore/caas/CloudHelp/cloudhelp/2019/ENU/3DSMax-Basics/files/GUID-1A97CFEC-60A3-4221-B9C3-5C808E2AED35-htm.html
            var startInfo = new ProcessStartInfo(this.exeFile, $"-ma -dfc -silent -vxs -U MAXScript {startupScriptFilename}")
            {
                WorkingDirectory = this.workDir,
                WindowStyle = ProcessWindowStyle.Minimized
            };
            this.Process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            //update UI
            if (this.Process != null)
            {
                this.Process.Exited += OnWorkerStopped;

                this.Process.Start();
                this.Process.PriorityClass = ProcessPriorityClass.Idle;

                this.timerProcessCheck = new Timer(OnProcessCheckTimer, null, TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(5));

                if (isRestart)
                {
                    this.restartCount ++;
                    this.Restarted?.Invoke(this, EventArgs.Empty);
                }
            }
        }

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
            this.timerProcessCheck?.Dispose();
            this.timerProcessCheck = null;

            var process = (Process)sender;
            process.Exited -= OnWorkerStopped;
            process.Dispose();

            this.Restart();
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