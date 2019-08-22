namespace AutoSync
{
    using System.Collections.Generic;
    using System.IO;

    using KeePass.Forms;
    using KeePass.Plugins;

    using KeePassLib;
    using KeePassLib.Interfaces;
    using KeePassLib.Serialization;

    using System;
    using System.Threading;

    using System.Windows.Forms; // For Debugging

    public class AutoSyncExt : Plugin
    {
        public override string UpdateUrl { get { return "https://raw.githubusercontent.com/darkretailer/Keepass.AutoSync/master/version"; } }
        private static IPluginHost host;

        class AutoSyncDB
        {
            public static Random randomizer = new Random();

            public PwDatabase database;
            public FileSystemWatcher fileSystemWatcher;
            
            public AutoSyncDB(PwDatabase database, AutoSyncExt parent)
            {
                this.database = database;
                var path = Path.GetDirectoryName(database.IOConnectionInfo.Path);
                var filename = Path.GetFileName(database.IOConnectionInfo.Path);
                this.fileSystemWatcher = new FileSystemWatcher(path, filename);
                this.fileSystemWatcher.Changed += parent.MonitorChanged;
                this.fileSystemWatcher.EnableRaisingEvents = true;
            }
            public void Sync()
            {
                this.fileSystemWatcher.EnableRaisingEvents = false;
                System.Threading.Thread.Sleep(500+randomizer.Next(500, 1000)+randomizer.Next(500, 1500));
                try {
                    var db = new PwDatabase();
                    db.Open(this.database.IOConnectionInfo, this.database.MasterKey, new NullStatusLogger());
                    this.database.MergeIn(db, PwMergeMethod.Synchronize);
                    db.Close();
                    AutoSyncExt.host.MainWindow.RefreshEntriesList();
                } catch(Exception e) {
                    this.database.Modified = true;
                    var notification = new System.Windows.Forms.NotifyIcon()
                    {
                        Visible = true,
                        Icon = System.Drawing.SystemIcons.Information,
                        BalloonTipIcon = System.Windows.Forms.ToolTipIcon.Warning,
                        BalloonTipTitle = "KeePass.AutoSync",
                        BalloonTipText = "Database: " + this.database.IOConnectionInfo.Path + Environment.NewLine + Environment.NewLine + e.Message,
                    };
                    notification.ShowBalloonTip(5000);
                    Thread.Sleep(10000);
                    notification.Dispose();
                }
                this.fileSystemWatcher.EnableRaisingEvents = true;
            }

            public void Dispose()
            {
                this.fileSystemWatcher.Dispose();
            }
        }
        private readonly IDictionary<string, AutoSyncDB> autoSyncDBList = new Dictionary<string, AutoSyncDB>();

        public override bool Initialize(IPluginHost host)
        {
            AutoSyncExt.host = host;

            AutoSyncExt.host.MainWindow.FileOpened += MainWindowOnFileOpened;
            AutoSyncExt.host.MainWindow.FileClosingPre += MainWindowOnFileClosingPre;
            
            return true;
        }

        private void MainWindowOnFileOpened(object sender, FileOpenedEventArgs fileOpenedEventArgs)
        {
            if (!fileOpenedEventArgs.Database.IOConnectionInfo.IsLocalFile())
            {
                return;
            }

            this.AddMonitor(fileOpenedEventArgs.Database);
        }

        private void MainWindowOnFileClosingPre(object sender, FileClosingEventArgs fileClosingEventArgs)
        {
            if (!fileClosingEventArgs.Database.IOConnectionInfo.IsLocalFile())
            {
                return;
            }

            this.RemoveMonitor(fileClosingEventArgs.Database);
        }

        private void AddMonitor(PwDatabase database)
        {
            if (autoSyncDBList.ContainsKey(database.IOConnectionInfo.Path))
            {
                return;
            }

            var path = Path.GetDirectoryName(database.IOConnectionInfo.Path);
            var filename = Path.GetFileName(database.IOConnectionInfo.Path);

            if (filename == null || path == null)
            {
                return;
            }

            var autoSyncDB = new AutoSyncDB(database, this);
            this.autoSyncDBList.Add(database.IOConnectionInfo.Path, autoSyncDB);
        }

        private void RemoveMonitor(PwDatabase database)
        {
            if (!autoSyncDBList.ContainsKey(database.IOConnectionInfo.Path))
            {
                return;
            }

            this.autoSyncDBList[database.IOConnectionInfo.Path].Dispose();
            this.autoSyncDBList.Remove(database.IOConnectionInfo.Path); 
        }

        private void MonitorChanged(object sender, FileSystemEventArgs e)
        {
            var watcher = sender as FileSystemWatcher;

            if (watcher == null)
            {
                return;
            }
            
            if (e.ChangeType != WatcherChangeTypes.Changed)
            {
                return;
            }

            try {
                this.autoSyncDBList[watcher.Path+"\\"+watcher.Filter].Sync();
            } catch (Exception ex) {
                MessageBox.Show("132"+ex.Message+" - "+watcher.Path+"\\"+watcher.Filter);
            }
        }

        public override void Terminate()
        {
            foreach (var autoSyncDB in this.autoSyncDBList.Values)
            {
                try {
                    autoSyncDB.Dispose();
                } catch(Exception e) {
                    MessageBox.Show(e.Message);
                }
            }
        }
    }
}