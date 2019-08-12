namespace AutoSync
{
    using System.Collections.Generic;
    using System.IO;

    using KeePass.Forms;
    using KeePass.Plugins;

    using KeePassLib;
    using KeePassLib.Interfaces;
    using KeePassLib.Serialization;

    using System.Windows.Forms;
    using System.Diagnostics;
    using System;
    using System.Threading;
    using System.Security.Permissions;

    public class AutoSyncExt : Plugin
    {
        private readonly IDictionary<string, FileSystemWatcher> watchers = new Dictionary<string, FileSystemWatcher>();

        private IPluginHost host;

        private Random randomizer = new Random();

        public override bool Initialize(IPluginHost host)
        {
            this.host = host;

            this.host.MainWindow.FileOpened += MainWindowOnFileOpened;
            this.host.MainWindow.FileClosingPre += MainWindowOnFileClosingPre;

            return true;
        }

        private void MainWindowOnFileOpened(object sender, FileOpenedEventArgs fileOpenedEventArgs)
        {
            if (!fileOpenedEventArgs.Database.IOConnectionInfo.IsLocalFile())
            {
                return;
            }

            this.AddMonitor(fileOpenedEventArgs.Database.IOConnectionInfo.Path);
        }

        private void MainWindowOnFileClosingPre(object sender, FileClosingEventArgs fileClosingEventArgs)
        {
            if (!fileClosingEventArgs.Database.IOConnectionInfo.IsLocalFile())
            {
                return;
            }

            this.RemoveMonitor(fileClosingEventArgs.Database.IOConnectionInfo.Path);
        }

        private void AddMonitor(string databaseFilename)
        {
            if (watchers.ContainsKey(databaseFilename))
            {
                return;
            }

            var path = Path.GetDirectoryName(databaseFilename);
            var filename = Path.GetFileName(databaseFilename);

            if (filename == null || path == null)
            {
                return;
            }

            var watcher = new FileSystemWatcher(path, filename);
            watcher.Changed += this.MonitorChanged;
            watcher.EnableRaisingEvents = true;

            this.watchers.Add(databaseFilename, watcher);
        }

        private void RemoveMonitor(string databaseFilename)
        {
            if (!watchers.ContainsKey(databaseFilename))
            {
                return;
            }

            this.watchers[databaseFilename].Dispose();
            this.watchers.Remove(databaseFilename);
        }

        private void MonitorChanged(object sender, FileSystemEventArgs e)
        {
            var watcher = sender as FileSystemWatcher;

            if (watcher == null)
            {
                return;
            }

            watcher.EnableRaisingEvents = false;
            
            if (e.ChangeType != WatcherChangeTypes.Changed)
            {
                return;
            }
            this.SyncDatabase(e.FullPath);
            watcher.EnableRaisingEvents = true;
        }

        private void SyncDatabase(string databaseFilename)
        {
            try {
                System.Threading.Thread.Sleep(500+this.randomizer.Next(500, 1000)+this.randomizer.Next(500, 1500));
                var db = new PwDatabase();

                db.Open(IOConnectionInfo.FromPath(databaseFilename), this.host.Database.MasterKey, new NullStatusLogger());

                this.host.Database.MergeIn(db, PwMergeMethod.Synchronize);

                db.Close();
			} catch(Exception e) {
				//MessageBox.Show(e.Message, "KeePass.AutoSync " + databaseFilename, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                var notification = new System.Windows.Forms.NotifyIcon()
                {
                    Visible = true,
                    Icon = System.Drawing.SystemIcons.Information,
                    BalloonTipIcon = System.Windows.Forms.ToolTipIcon.Warning,
                    BalloonTipTitle = "KeePass.AutoSync ",
                    BalloonTipText = "Datenbank: " + databaseFilename + Environment.NewLine + e.Message,
                };
                notification.ShowBalloonTip(5000);
                Thread.Sleep(10000);
                notification.Dispose();
			}
            this.host.MainWindow.RefreshEntriesList();
        }
        public override void Terminate()
        {
            foreach (var watcher in this.watchers.Values)
            {
                watcher.Dispose();
            }
        }
    }
}