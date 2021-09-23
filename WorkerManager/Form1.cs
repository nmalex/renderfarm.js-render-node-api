using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace WorkerManager
{
    public partial class Form1 : Form
    {
        private bool appExiting;
        private bool tooltipShown;

        private ListViewItem selectedItem;
        private readonly IDictionary<IWorker, ListViewItem> listItemsMap = new Dictionary<IWorker, ListViewItem>();

        private readonly IWorkersManager workersManager;
        private readonly Thread thread1;
        private bool thread1Running = true;
        private readonly WorkerManagerEndpoint endpoint;
        // private readonly Configuration config;
        private Process spawnerProcess;
        private readonly System.Threading.Timer heartbeatTimer;
        private readonly IPAddress controllerHost;
        private IPAddress localIp;
        private string localMac;
        private Socket heartbeatSocket;
        private IPEndPoint heartbeatEndpoint;
        private static int HeartbeatI;
        private readonly PerformanceCounter totalCpuCounter;
        // private readonly float physicalRam;
        private readonly string currentVersion;

        static readonly Func<float, float> ToGb = val => val / 1024.0f / 1024.0f / 1024.0f;
        private readonly Settings settings;

        public Form1()
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var exc = (Exception) args.ExceptionObject;
                var sb = new StringBuilder();
                sb.AppendLine(exc.Message);
                sb.AppendLine(exc.StackTrace);

                File.WriteAllText("C:\\Temp\\WorkerManager.log", sb.ToString());
            };

            InitializeComponent();
            SetProcessPrio(ProcessPriorityClass.High);

            // ReSharper disable once AssignNullToNotNullAttribute
            this.settings = new Settings(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "settings.json"));
            this.settings.Load();

            this.currentVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            this.thread1 = new Thread(ThreadStart1);
            this.thread1.Start(null);

            this.workersManager = new WorkersManager(this.settings);
            this.workersManager.Added += OnWorkerAdded;
            this.workersManager.Updated += OnWorkerUpdated;
            this.workersManager.Deleted += OnWorkerDeleted;
            this.workersManager.Load();

            this.endpoint = new WorkerManagerEndpoint(this.workersManager);

            var ip = (string) this.settings["listen_ip"];
            var port = (long) this.settings["listen_port"];
            this.endpoint.Listen(ip, (int)port);

            var endpointUrl = $"http://localhost:{port}/worker";
            this.linkEndpoint.Text = endpointUrl;
            this.linkEndpoint.LinkClicked += (sender, args) =>
            {
                Process.Start("explorer.exe", $"\"{endpointUrl}\"");
            };

            this.cbSpawner.Checked = (bool) this.settings["run_spawner"];

            // ReSharper disable once CoVariantArrayConversion
            this.comboBox1.Items.AddRange(this.settings["available_controllers"].Values<string>().ToArray());
            this.comboBox1.SelectedItem = (string)this.settings["controller_host"];
            this.comboBox1.SelectedValueChanged += (sender, args) =>
            {
                this.settings["controller_host"] = this.comboBox1.SelectedItem.ToString();
                this.settings.Save();
                this.workersManager.RestartAll();
            };

            // ReSharper disable once CoVariantArrayConversion
            this.cbWorkgroup.Items.AddRange(this.settings["available_workgroups"].Values<string>().ToArray());
            this.cbWorkgroup.SelectedItem = (string)this.settings["workgroup"];
            this.cbWorkgroup.SelectedValueChanged += (sender, args) =>
            {
                this.settings["workgroup"] = this.cbWorkgroup.SelectedItem.ToString();
                this.settings.Save();
                this.workersManager.RestartAll();
            };

            this.totalCpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");

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


            this.heartbeatTimer = new System.Threading.Timer(SendHeartbeat, null, TimeSpan.FromMilliseconds(150), TimeSpan.FromSeconds(1));
            NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;

            var assembly = Assembly.GetExecutingAssembly();
            var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            var versionStr = "ver." + fvi.FileVersion;
            var externalIpStr = " - ext ip=" + (string)this.settings["worker_external_ip"];
            this.Text = string.Format("RFarm Worker Manager - {0}{1}", versionStr, externalIpStr);

            this.ShowWindow();
        }

        private void GetPhysicalRamInstalled(out float usedRam, out float totalRam)
        {
            usedRam = 0;
            totalRam = 0;

            var memStatus = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(memStatus))
            {
                totalRam = ToGb(memStatus.ullTotalPhys);
                usedRam = ToGb(memStatus.ullTotalPhys - memStatus.ullAvailPhys);
            }
        }

        private void InitializeHeartbeat()
        {
            this.heartbeatSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            var heartbeatPort = (long)this.settings["controller_port"];
            this.heartbeatEndpoint = new IPEndPoint(this.controllerHost, (int)heartbeatPort);

            this.UpdateNetworkAddress();
        }

        private void UpdateNetworkAddress()
        {
            var networkInterface = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up
                            && (n.NetworkInterfaceType == NetworkInterfaceType.Ethernet 
                                || n.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet 
                                || n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                            && n.GetIPProperties().UnicastAddresses.Any(a => a.Address.AddressFamily == AddressFamily.InterNetwork));

            this.localIp = networkInterface?.GetIPProperties()?.UnicastAddresses?.FirstOrDefault()?.Address;
            this.localMac = networkInterface?.GetPhysicalAddress().ToString().ToLower();
        }

        private void OnNetworkAddressChanged(object sender, EventArgs e)
        {
            UpdateNetworkAddress();
        }

        private void SendHeartbeat(object state)
        {
            if (this.heartbeatSocket == null || !this.heartbeatSocket.Connected)
            {
                this.heartbeatSocket?.Dispose();
                this.InitializeHeartbeat();
            }

            float usedRam;
            float totalRam;
            GetPhysicalRamInstalled(out usedRam, out totalRam);

            var runningVraySpawner = this.spawnerProcess != null && !this.spawnerProcess.HasExited;
            var cpuUsage = this.totalCpuCounter.NextValue().ToString("0.000", CultureInfo.InvariantCulture);
            var externalIp = (string)this.settings["worker_external_ip"];
            var message = $"{{\"id\":{++HeartbeatI}, \"type\":\"heartbeat\", \"sender\":\"worker-manager\", \"version\":\"{this.currentVersion}\", \"external_ip\":\"{externalIp}\", \"mac\":\"{this.localMac}\", \"vray_spawner\":{runningVraySpawner.ToString().ToLower()}, \"worker_count\":{this.workersManager.Count}, \"worker_progress\":{this.GetWorkerProgressJsonStr()}, \"cpu_usage\":{cpuUsage}, \"ram_usage\":{usedRam.ToString("0.000", CultureInfo.InvariantCulture)}, \"total_ram\":{totalRam.ToString("0.000", CultureInfo.InvariantCulture)}}}";
            var sendBuffer = Encoding.ASCII.GetBytes(message);
            this.heartbeatSocket.SendTo(sendBuffer, this.heartbeatEndpoint);
        }

        private string GetWorkerProgressJsonStr()
        {
            var jsons = this.workersManager.Workers.Select(i => $"{{ \"pid\": {i.Pid ?? 0}, \"port\": {i.Port}, \"vray_progress\": \"{i.VrayProgress}\" }}").ToArray();
            return "[ " + (string.Join(", ", jsons)) + " ]";
        }

        private void ThreadStart1(object obj)
        {
            while (this.thread1Running)
            {
                // First application
                var waitForSignal = new EventWaitHandle(false, EventResetMode.AutoReset, "MyWaitHandle");

                // Here, the first application does whatever initialization it can.
                // Then it waits for the handle to be signaled:
                // The program will block until somebody signals the handle.
                if (waitForSignal.WaitOne(TimeSpan.FromSeconds(1)))
                {
                    try
                    {
                        this.Invoke((MethodInvoker) ShowWindow);
                    }
                    catch (ObjectDisposedException)
                    {
                        // ignore it
                    }
                }
            }
        }

        private void OnWorkerAdded(object sender, IWorker workerAdded)
        {
            var listViewItem = new ListViewItem
            {
                Text = workerAdded.Label,
                Tag = workerAdded,
                ImageIndex = 0
            };

            this.listView1.SafeInvoke(() =>
            {
                this.listItemsMap[workerAdded] = listViewItem;
                this.listView1.Items.Add(listViewItem);
            });

            this.UpdateWorkerCount();
        }

        private void OnWorkerUpdated(object sender, IWorker workerUpdated)
        {
            ListViewItem listItem;
            if (this.listItemsMap.TryGetValue(workerUpdated, out listItem))
            {
                this.listView1.SafeInvoke(() =>
                {
                    listItem.Text = workerUpdated.Label;
                });
            }
        }

        private void OnWorkerDeleted(object sender, IWorker workerDeleted)
        {
            var deletedListItem = this.listItemsMap[workerDeleted];
            this.listView1.Items.Remove(deletedListItem);

            this.UpdateWorkerCount();
        }

        private void UpdateWorkerCount()
        {
            this.lblWorkersCount.SafeInvoke(() =>
            {
                this.lblWorkersCount.Text = $"Worker Count: {this.workersManager.Count}";
            });
        }

        private static void SetProcessPrio(ProcessPriorityClass prio)
        {
            using (var p = Process.GetCurrentProcess())
            {
                p.PriorityClass = prio;
            }
        }

        private void NotifyIcon1OnBalloonTipClicked(object sender, EventArgs eventArgs)
        {
            this.ShowWindow();
            this.notifyIcon1.BalloonTipClicked -= NotifyIcon1OnBalloonTipClicked;
        }

        private void ShowWindow()
        {
            Show();
            this.WindowState = FormWindowState.Normal;
            this.notifyIcon1.Visible = true;
            this.BringToFront();
        }

        private void MinimizeToSystemTray()
        {
            this.TopMost = false;

            Hide();
            this.notifyIcon1.Visible = true;
            if (!this.tooltipShown)
            {
                this.tooltipShown = true;
                this.notifyIcon1.ShowBalloonTip(3000); //todo: extract to App.config
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.notifyIcon1.BalloonTipClicked += NotifyIcon1OnBalloonTipClicked;

            listView1.View = View.Details;
            listView1.HeaderStyle = ColumnHeaderStyle.Clickable;
            listView1.Columns.Add("worker", "Worker", 200, HorizontalAlignment.Right, 0);
            //listView1.Columns.Add("ram", "RAM", 100, HorizontalAlignment.Right, -1);
            //listView1.Columns.Add("cpu", "CPU", 100, HorizontalAlignment.Right, -1);

            this.listView1.SmallImageList = new ImageList
            {
                ImageSize = new Size(16, 16),
                ColorDepth = ColorDepth.Depth32Bit
            };
            this.listView1.SmallImageList.Images.Add(new Icon("server.ico"));

            this.BringToFront();
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.MinimizeToSystemTray();
            }
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            ShowWindow();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!this.appExiting)
            {
                this.MinimizeToSystemTray();
                e.Cancel = true;
            }
        }

        private void btnExitApp_Click(object sender, EventArgs e)
        {
            this.ExitApp();
        }

        private void ExitApp()
        {
            DisableUi();
            this.listView1.Items.Clear();

            this.appExiting = true;
            this.heartbeatTimer.Dispose();

            NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
            this.heartbeatSocket?.Close();
            this.heartbeatSocket?.Dispose();

            if (this.spawnerProcess != null)
            {
                this.spawnerProcess.Exited -= OnSpawnerExit;
                KillProcessAndChildren(this.spawnerProcess.Id);
                this.spawnerProcess = null;
            }

            this.workersManager.Close();

            this.thread1Running = false;
            this.thread1.Join();

            this.endpoint.Close();

            this.notifyIcon1.Visible = false;
            this.Close();
        }

        private void DisableUi()
        {
            this.btnExitApp.Enabled = false;
            this.btnAddWorker.Enabled = false;
            this.btnDeleteWorker.Enabled = false;
            this.cbSpawner.Enabled = false;
            this.linkEndpoint.Visible = false;
            this.btnSettings.Enabled = false;
            this.listView1.Enabled = false;
        }

        private void listView1_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            this.selectedItem = e.IsSelected ? e.Item : null;
            this.btnDeleteWorker.Enabled = this.selectedItem != null;
        }

        private void btnAddWorker_Click(object sender, EventArgs e)
        {
            this.workersManager.AddWorker();
        }

        private void btnDeleteWorker_Click(object sender, EventArgs e)
        {
            this.workersManager.DeleteWorker((IWorker)this.selectedItem.Tag);
        }

        private void listView1_DoubleClick(object sender, EventArgs e)
        {
            var selectedWorker = this.selectedItem?.Tag as IWorker;
            if (selectedWorker?.Pid != null)
            {
                selectedWorker.BringToFront();
                this.TopMost = true;
            }
        }

        private void btnSettings_Click(object sender, EventArgs e)
        {
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (dir == null) return;

            var lastWriteTimeBefore = File.GetLastWriteTime(this.settings.FilePath);
            var startInfo = new ProcessStartInfo
            {
                FileName = "notepad.exe",
                Arguments = this.settings.FilePath,
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System)
            };
            var process = Process.Start(startInfo);
            if (process != null)
            {
                process.EnableRaisingEvents = true;
                process.Exited += (o, args) =>
                {
                    this.CheckAppConfigChange(lastWriteTimeBefore);
                };
            }
        }

        private void CheckAppConfigChange(DateTime lastWriteTimeBefore)
        {
            var lastWriteTimeAfter = File.GetLastWriteTime(this.settings.FilePath);
            if (DateTime.Equals(lastWriteTimeAfter, lastWriteTimeBefore))
            {
                //user did not save file
                return;
            }

            this.SafeInvoke(ExitApp, p =>
            {
                var newInstanceOfWorkerManager = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = Path.GetFileName(Assembly.GetExecutingAssembly().Location),
                        // ReSharper disable once AssignNullToNotNullAttribute
                        WorkingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                        Arguments = "/restart"
                    }
                };
                newInstanceOfWorkerManager.Start();
            });
        }

        private void cbSpawner_CheckedChanged(object sender, EventArgs e)
        {
            this.settings["run_spawner"] = this.cbSpawner.Checked;
            this.settings.Save();

            if (this.cbSpawner.Checked)
            {
                this.StartVraySpawner();
            }
            else
            {
                if (this.spawnerProcess != null)
                {
                    this.spawnerProcess.EnableRaisingEvents = false;
                    this.spawnerProcess.Exited -= OnSpawnerExit;
                    KillProcessAndChildren(this.spawnerProcess.Id);
                    this.spawnerProcess = null;
                }
            }
        }

        private void StartVraySpawner()
        {
            this.spawnerProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = (string)this.settings["vrayspawner_exe"],
                    WorkingDirectory = (string)this.settings["3dsmax_dir"]
                },
                EnableRaisingEvents = true
            };
            this.spawnerProcess.Exited += OnSpawnerExit;
            this.spawnerProcess.Start();
        }

        private void OnSpawnerExit(object sender, EventArgs e)
        {
            this.spawnerProcess.Exited -= OnSpawnerExit;
            Thread.Sleep(1000);
            StartVraySpawner();
        }

        private static void KillProcessAndChildren(int pid)
        {
            var taskkill = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "taskkill.exe",
                    WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System),
                    Arguments = $"/PID {pid} /T /F",
                    WindowStyle = ProcessWindowStyle.Minimized
                }
            };
            taskkill.Start();
            taskkill.WaitForExit();
        }

#pragma warning disable 169
#pragma warning disable 649
        // ReSharper disable ClassNeverInstantiated.Local
        // ReSharper disable InconsistentNaming
        // ReSharper disable MemberCanBePrivate.Local
        // ReSharper disable NotAccessedField.Local
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
            public MEMORYSTATUSEX()
            {
                this.dwLength = (uint)Marshal.SizeOf(typeof(NativeMethods.MEMORYSTATUSEX));
            }
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);
    }
}
