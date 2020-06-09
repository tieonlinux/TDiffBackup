using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using deltaq_tie.BsDiff;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using TerrariaApi.Server;
using TShockAPI;

#nullable enable

namespace DiffBackup
{
    [ApiVersion(2, 1)]
    public class DiffBackupPlugin : TerrariaPlugin
    {
        /// <summary>
        /// Gets the author(s) of this plugin
        /// </summary>
        public override string Author => "Tieonlinux";

        /// <summary>
        /// Gets the description of this plugin.
        /// A short, one lined description that tells people what your plugin does.
        /// </summary>
        public override string Description => "A sample test plugin";

        /// <summary>
        /// Gets the name of this plugin.
        /// </summary>
        public override string Name => "TDiffBackup";

        /// <summary>
        /// Gets the version of this plugin.
        /// </summary>
        public override Version Version => GetType().Assembly.GetName().Version;

        # region private fields

        private readonly IBackupService _backupService;

        private readonly FileSystemWatcher _watcher = new FileSystemWatcher {IncludeSubdirectories = false};
        private Stopwatch _lastWriteStopwatch = new Stopwatch();

        #endregion

        /// <summary>
        /// Initializes a new instance of the TestPlugin class.
        /// This is where you set the plugin's order and perfrom other constructor logic
        /// </summary>
        public DiffBackupPlugin(Main game) : base(game)
        {
            Order = 10;
            _backupService = new BackupService(this.Logger());
        }

        /// <summary>
        /// Handles plugin initialization. 
        /// Fired when the server is started and the plugin is being loaded.
        /// You may register hooks, perform loading procedures etc here.
        /// </summary>
        public override void Initialize()
        {
            this.LogDebug("Initialize");
            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            ServerApi.Hooks.GamePostInitialize.Register(this, OnPostInitialize);
            ServerApi.Hooks.WorldSave.Register(this, OnWorldSaved);
        }


        /// <summary>
        /// Handles plugin disposal logic.
        /// *Supposed* to fire when the server shuts down.
        /// You should deregister hooks and free all resources here.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.LogDebug("Removing all hooks");
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                ServerApi.Hooks.GameInitialize.Deregister(this, OnPostInitialize);
                ServerApi.Hooks.WorldSave.Deregister(this, OnWorldSaved);

                _backupService.Dispose();
                _watcher.Dispose();
            }

            base.Dispose(disposing);
        }
        

        private void OnInitialize(EventArgs args)
        {
            //LogDebug("Adding chat commands");
            Commands.ChatCommands.Add(new Command("tdiff", TdiffCmd, "action", "date"));
        }
        
        private void TdiffCmd(CommandArgs args)
        {
            if (args.Parameters.Count == 0 || args.Parameters[0].ToLowerInvariant() == "help")
            {
                args.Player.SendInfoMessage("/tdiff ls <date> - to list backups");
                args.Player.SendInfoMessage("/tdiff <date> - to restore closest backup @ date");
                return;
            }

            if (args.Parameters[0].ToLowerInvariant() == "ls" || args.Parameters[0].ToLowerInvariant() == "list")
            {
                var date = DateTime.Now;
                if (args.Parameters.Count > 1)
                {
                    //todo parse date
                }
                
            }
        }

        private void OnPostInitialize(EventArgs args)
        {
            _watcher.NotifyFilter |= NotifyFilters.LastWrite;
            _watcher.Filter = Path.GetFileName(Main.worldPathName);
            _watcher.Path = Path.GetDirectoryName(Main.worldPathName);
            _watcher.Changed += OnFileChanged;
            _watcher.EnableRaisingEvents = true;
        }

        private void OnWorldSaved(EventArgs args) => OnFileChanged(this,
            new FileSystemEventArgs(WatcherChangeTypes.Changed, Path.GetDirectoryName(Main.worldPathName),
                Path.GetFileName(Main.worldPathName)));

        private void OnFileChanged(object source, FileSystemEventArgs e)
        {
            if (TShock.ShuttingDown) return;
            this.LogDebug("OnFileChanged " + e.FullPath);
            var debounceDelay = TimeSpan.FromSeconds(5);
            var precisionCorrection = TimeSpan.FromSeconds(1.0 / 30);
            _lastWriteStopwatch = Stopwatch.StartNew();

            async Task RecheckLaterAsync()
            {
                await Task.Delay(debounceDelay);
                if (TShock.ShuttingDown) return;
                if (_lastWriteStopwatch.Elapsed < debounceDelay - precisionCorrection)
                {
                    return;
                }

                try
                {
                    //try opening the file
                    using var _ = File.OpenRead(e.FullPath);
                }
                catch (IOException ex)
                {
                    this.LogWarn($"Unable to open world file {e.Name}");
                    this.LogDebug(ex.StackTrace, TraceLevel.Error);
                    return;
                }
                await _backupService.StartBackup(e.FullPath, CancellationToken.None);
            }

            Task.Factory.StartNew(async () => await RecheckLaterAsync());
        }
    }
}