namespace WorkerManager
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.notifyIcon1 = new System.Windows.Forms.NotifyIcon(this.components);
            this.btnExitApp = new System.Windows.Forms.Button();
            this.listView1 = new System.Windows.Forms.ListView();
            this.btnAddWorker = new System.Windows.Forms.Button();
            this.btnDeleteWorker = new System.Windows.Forms.Button();
            this.lblWorkersCount = new System.Windows.Forms.Label();
            this.linkEndpoint = new System.Windows.Forms.LinkLabel();
            this.btnSettings = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // notifyIcon1
            // 
            this.notifyIcon1.BalloonTipText = "The app will run minimized";
            this.notifyIcon1.BalloonTipTitle = "RFarm Worker Manager";
            this.notifyIcon1.Icon = ((System.Drawing.Icon)(resources.GetObject("notifyIcon1.Icon")));
            this.notifyIcon1.Text = "RFarm Worker Manager";
            this.notifyIcon1.Visible = true;
            this.notifyIcon1.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.notifyIcon1_MouseDoubleClick);
            // 
            // btnExitApp
            // 
            this.btnExitApp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnExitApp.Location = new System.Drawing.Point(777, 530);
            this.btnExitApp.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.btnExitApp.Name = "btnExitApp";
            this.btnExitApp.Size = new System.Drawing.Size(75, 28);
            this.btnExitApp.TabIndex = 0;
            this.btnExitApp.Text = "Exit";
            this.btnExitApp.UseVisualStyleBackColor = true;
            this.btnExitApp.Click += new System.EventHandler(this.btnExitApp_Click);
            // 
            // listView1
            // 
            this.listView1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listView1.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.listView1.Location = new System.Drawing.Point(12, 7);
            this.listView1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.listView1.Name = "listView1";
            this.listView1.Size = new System.Drawing.Size(840, 500);
            this.listView1.TabIndex = 2;
            this.listView1.UseCompatibleStateImageBehavior = false;
            this.listView1.ItemSelectionChanged += new System.Windows.Forms.ListViewItemSelectionChangedEventHandler(this.listView1_ItemSelectionChanged);
            this.listView1.DoubleClick += new System.EventHandler(this.listView1_DoubleClick);
            // 
            // btnAddWorker
            // 
            this.btnAddWorker.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnAddWorker.Location = new System.Drawing.Point(12, 529);
            this.btnAddWorker.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.btnAddWorker.Name = "btnAddWorker";
            this.btnAddWorker.Size = new System.Drawing.Size(75, 28);
            this.btnAddWorker.TabIndex = 3;
            this.btnAddWorker.Text = "Add";
            this.btnAddWorker.UseVisualStyleBackColor = true;
            this.btnAddWorker.Click += new System.EventHandler(this.btnAddWorker_Click);
            // 
            // btnDeleteWorker
            // 
            this.btnDeleteWorker.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnDeleteWorker.Enabled = false;
            this.btnDeleteWorker.Location = new System.Drawing.Point(93, 529);
            this.btnDeleteWorker.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.btnDeleteWorker.Name = "btnDeleteWorker";
            this.btnDeleteWorker.Size = new System.Drawing.Size(75, 28);
            this.btnDeleteWorker.TabIndex = 4;
            this.btnDeleteWorker.Text = "Delete";
            this.btnDeleteWorker.UseVisualStyleBackColor = true;
            this.btnDeleteWorker.Click += new System.EventHandler(this.btnDeleteWorker_Click);
            // 
            // lblWorkersCount
            // 
            this.lblWorkersCount.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lblWorkersCount.AutoSize = true;
            this.lblWorkersCount.Location = new System.Drawing.Point(173, 537);
            this.lblWorkersCount.Name = "lblWorkersCount";
            this.lblWorkersCount.Size = new System.Drawing.Size(111, 17);
            this.lblWorkersCount.TabIndex = 8;
            this.lblWorkersCount.Text = "Worker Count: 0";
            // 
            // linkEndpoint
            // 
            this.linkEndpoint.AutoSize = true;
            this.linkEndpoint.Location = new System.Drawing.Point(293, 537);
            this.linkEndpoint.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.linkEndpoint.Name = "linkEndpoint";
            this.linkEndpoint.Size = new System.Drawing.Size(146, 17);
            this.linkEndpoint.TabIndex = 9;
            this.linkEndpoint.TabStop = true;
            this.linkEndpoint.Text = "http://localhost/worker";
            // 
            // btnSettings
            // 
            this.btnSettings.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSettings.Location = new System.Drawing.Point(685, 530);
            this.btnSettings.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.btnSettings.Name = "btnSettings";
            this.btnSettings.Size = new System.Drawing.Size(86, 28);
            this.btnSettings.TabIndex = 10;
            this.btnSettings.Text = "Settings…";
            this.btnSettings.UseVisualStyleBackColor = true;
            this.btnSettings.Click += new System.EventHandler(this.btnSettings_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(864, 570);
            this.Controls.Add(this.btnSettings);
            this.Controls.Add(this.linkEndpoint);
            this.Controls.Add(this.lblWorkersCount);
            this.Controls.Add(this.btnDeleteWorker);
            this.Controls.Add(this.btnAddWorker);
            this.Controls.Add(this.listView1);
            this.Controls.Add(this.btnExitApp);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.Name = "Form1";
            this.Text = "RFarm Worker Manager";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            this.Load += new System.EventHandler(this.Form1_Load);
            this.Resize += new System.EventHandler(this.Form1_Resize);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.NotifyIcon notifyIcon1;
        private System.Windows.Forms.Button btnExitApp;
        private System.Windows.Forms.ListView listView1;
        private System.Windows.Forms.Button btnAddWorker;
        private System.Windows.Forms.Button btnDeleteWorker;
        private System.Windows.Forms.Label lblWorkersCount;
        private System.Windows.Forms.LinkLabel linkEndpoint;
        private System.Windows.Forms.Button btnSettings;
    }
}

