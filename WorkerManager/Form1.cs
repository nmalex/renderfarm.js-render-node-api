using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
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
        private readonly Configuration config;
        private Process spawnerProcess;

        // ReSharper disable once InconsistentNaming
        private const int TCP_MANAGEMENT_PORT = 17900;

        public Form1()
        {
            InitializeComponent();
            SetProcessPrio(ProcessPriorityClass.High);

            this.config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

            this.thread1 = new Thread(ThreadStart1);
            this.thread1.Start(null);

            this.workersManager = new WorkersManager(config);
            this.workersManager.Added += OnWorkerAdded;
            this.workersManager.Updated += OnWorkerUpdated;
            this.workersManager.Deleted += OnWorkerDeleted;
            this.workersManager.Load();

            this.endpoint = new WorkerManagerEndpoint(this.workersManager);
            this.endpoint.Listen(TCP_MANAGEMENT_PORT);

            var endpointUrl = $"http://localhost:{TCP_MANAGEMENT_PORT}/worker";
            this.linkEndpoint.Text = endpointUrl;
            this.linkEndpoint.LinkClicked += (sender, args) =>
            {
                Process.Start("explorer.exe", $"\"{endpointUrl}\"");
            };

            bool runSpawner;
            if (bool.TryParse(config.AppSettings.Settings["run_spawner"].Value, out runSpawner))
            {
                this.cbSpawner.Checked = runSpawner;
            }
            else
            {
                config.AppSettings.Settings["run_spawner"].Value = "false";
                config.Save(ConfigurationSaveMode.Modified);
            }

            this.ShowWindow();
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
                this.notifyIcon1.ShowBalloonTip(3000);
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

            this.appExiting = true;

            if (this.spawnerProcess != null)
            {
                this.spawnerProcess.Exited -= OnSpawnerExit;
                this.spawnerProcess.Kill();
                this.spawnerProcess = null;
            }

            this.workersManager.Close();

            this.notifyIcon1.Visible = false;

            this.thread1Running = false;
            this.thread1.Join();

            this.endpoint.Close();

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

            var filename = config.FilePath;
            var lastWriteTimeBefore = File.GetLastWriteTime(filename);
            var startInfo = new ProcessStartInfo
            {
                FileName = "notepad.exe",
                Arguments = filename,
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System)
            };
            var process = Process.Start(startInfo);
            if (process != null)
            {
                process.EnableRaisingEvents = true;
                process.Exited += (o, args) =>
                {
                    var lastWriteTimeAfter = File.GetLastWriteTime(filename);
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
                };
            }
        }

        private void cbSpawner_CheckedChanged(object sender, EventArgs e)
        {
            config.AppSettings.Settings["run_spawner"].Value = this.cbSpawner.Checked.ToString();
            config.Save(ConfigurationSaveMode.Modified);

            if (this.cbSpawner.Checked)
            {
                this.StartVraySpawner();
            }
            else
            {
                if (this.spawnerProcess != null)
                {
                    this.spawnerProcess.Exited -= OnSpawnerExit;
                    this.spawnerProcess.Kill();
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
                    FileName = config.AppSettings.Settings["spawner_exe"].Value,
                    WorkingDirectory = config.AppSettings.Settings["work_dir"].Value
                },
                EnableRaisingEvents = true
            };
            this.spawnerProcess.Exited += OnSpawnerExit;
            this.spawnerProcess.Start();
        }

        private void OnSpawnerExit(object sender, EventArgs e)
        {
            this.spawnerProcess.Exited -= OnSpawnerExit;
            StartVraySpawner();
        }
    }
}
