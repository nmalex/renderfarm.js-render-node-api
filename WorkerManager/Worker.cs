using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

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
        private readonly string iniFile;
        private readonly string workDir;

        public Worker(string ip, int port, string controllerHost, string exeFile, string iniFile, string workDir)
        {
            this.Ip = ip;
            this.Port = port;

            this.controllerHost = controllerHost;
            this.exeFile = exeFile;
            this.iniFile = iniFile;
            this.workDir = workDir;
        }

        #region Properties

        private string Ip { get; }

        public int Port { get; }

        public string Label => $"{this.Ip}:{this.Port}";

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
            //prepare startup.ms file for this worker
            var startupScriptFilename = Path.Combine(Path.GetTempPath(), $"worker_{this.Port}.ms");
            File.WriteAllText(startupScriptFilename, $"threejsApiStart {this.Port} \"{this.controllerHost}\"");

            //start worker process with parameters
            //learn more about command line parameters here: 
            //https://knowledge.autodesk.com/support/3ds-max/learn-explore/caas/CloudHelp/cloudhelp/2019/ENU/3DSMax-Basics/files/GUID-1A97CFEC-60A3-4221-B9C3-5C808E2AED35-htm.html
            var startInfo = new ProcessStartInfo(this.exeFile, $"-ma -dfc -silent -vxs -i {this.iniFile} -U MAXScript {startupScriptFilename}")
            {
                WorkingDirectory = this.workDir,
                WindowStyle = ProcessWindowStyle.Minimized
            };
            this.Process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };
            this.Process.Start();

            //update UI
            if (this.Process != null)
            {
                this.Process.PriorityClass = ProcessPriorityClass.Idle;
                this.Process.Exited += OnWorkerStopped;
            }
        }

        public void Kill()
        {
            if (this.Process != null)
            {
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
            var process = (Process)sender;
            process.Exited -= OnWorkerStopped;
            process.Dispose();

            this.Start();
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