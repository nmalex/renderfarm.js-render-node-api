using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace WorkerManager
{
    public class WorkersManager : IWorkersManager
    {
        private readonly Settings settings;
        private readonly Dictionary<int, IWorker> workers = new Dictionary<int, IWorker>();


        static readonly Random Rnd = new Random();

        private readonly object sync = new object();

        public event EventHandler<IWorker> Added;
        public event EventHandler<IWorker> Deleted;
        public event EventHandler<IWorker> Updated;

        public int Count
        {
            get
            {
                lock (this.sync)
                {
                    return this.workers.Count;
                }
            }
        }

        public IEnumerable<IWorker> Workers
        {
            get
            {
                lock (this.sync)
                {
                    return this.workers.Values.ToList();
                }
            }
        }

        public WorkersManager(Settings settings)
        {
            this.settings = settings;
        }

        public IWorker AddWorker()
        {
            IWorker worker;
            lock (this.sync)
            {
                worker = this.CreateWorker();
                this.workers[worker.Port] = worker;
                this.SetWorkerCountSettings(this.workers.Count);
            }

            worker.Restarted += this.OnWorkerRestarted;
            worker.ProgressChanged += this.OnWorkerProgressChanged;
            worker.Start();

            this.Added?.Invoke(this, worker);
            return worker;
        }

        private void OnWorkerRestarted(object sender, EventArgs e)
        {
            this.Updated?.Invoke(this, (IWorker)sender);
        }

        private void OnWorkerProgressChanged(object sender, string e)
        {
            this.Updated?.Invoke(this, (IWorker)sender);
        }

        public void DeleteWorker(IWorker worker)
        {
            lock (this.sync)
            {
                if (!this.workers.ContainsKey(worker.Port))
                {
                    return;
                }

                this.workers.Remove(worker.Port);
                this.SetWorkerCountSettings(this.workers.Count);
            }

            worker.Restarted -= this.OnWorkerRestarted;
            worker.Kill();
            worker.Dispose();

            this.Deleted?.Invoke(this, worker);
        }

        public void Load()
        {
            var workerCount = (long)this.settings["worker_count"];

            for (var i = 0; i < workerCount; i++)
            {
                this.AddWorker();
            }
        }

        private IWorker CreateWorker()
        {
            Worker worker;

            var workerPortRangeFrom = (long)this.settings["worker_port_range_from"];
            var wokerPortRangeWidth = (long)this.settings["worker_port_range_width"];

            lock (this.sync)
            {
                do
                {
                    var randomPort = (int)Math.Floor(workerPortRangeFrom + wokerPortRangeWidth * Rnd.NextDouble());
                    var ip = GetLocalIp();
                    worker = new Worker(ip, randomPort, this.settings);
                } while (this.workers.ContainsKey(worker.Port));
            }

            return worker;
        }

        private string GetLocalIp()
        {
            var ifs = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(i => i.OperationalStatus == OperationalStatus.Up
                                     && i.GetIPProperties()?.UnicastAddresses.Count > 0
                                     &&
                                     i.GetIPProperties()
                                         .UnicastAddresses.Any(
                                             a => a.Address.AddressFamily == AddressFamily.InterNetwork));

            return ifs?
                .GetIPProperties()
                .UnicastAddresses
                .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)?
                .Address
                .ToString();
        }

        private void SetWorkerCountSettings(int workerCount)
        {
            this.settings["worker_count"] = workerCount;
            this.settings.Save();
        }

        // ReSharper disable once UnusedMember.Local
        private float GetPhysicalRam()
        {
            var memStatus = new NativeMethods.MEMORYSTATUSEX();
            if (!NativeMethods.GlobalMemoryStatusEx(memStatus))
            {
                return 0.0f;
            }

            return memStatus.ullTotalPhys / 1024.0f / 1024.0f / 1024.0f;
        }

        public void Close()
        {
            List<IWorker> workersCopy;
            lock (this.sync)
            {
                workersCopy = this.workers.Values.ToList();
                this.workers.Clear();
            }

            foreach (var worker in workersCopy)
            {
                worker.Kill();
                worker.Dispose();
            }
        }

        public bool DeleteWorker(int port)
        {
            lock (this.sync)
            {
                var worker2Delete = this.workers.Values.FirstOrDefault(w => w.Port == port);
                if (worker2Delete == null)
                {
                    return false;
                }

                this.DeleteWorker(worker2Delete);

                return true;
            }
        }

        public bool KillWorker(int port)
        {
            lock (this.sync)
            {
                var worker2Kill = this.workers.Values.FirstOrDefault(w => w.Port == port);
                if (worker2Kill == null)
                {
                    return false;
                }

                if (worker2Kill.Pid != null)
                {
                    var workerProcess = Process.GetProcessById((int) worker2Kill.Pid, Environment.MachineName);
                    workerProcess.Kill();
                }

                return true;
            }
        }

        public void RestartAll()
        {
            lock (this.sync)
            {
                foreach (var w in this.workers)
                {
                    if (w.Value.Pid != null)
                    {
                        var workerProcess = Process.GetProcessById(w.Value.Pid.Value, Environment.MachineName);
                        workerProcess.Kill();
                    }
                }
            }
        }
    }
}