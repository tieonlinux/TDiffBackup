using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DiffBackup.Backup;
using DiffBackup.Logger;
using HumanDateParser_tie;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Extensions;

#nullable enable

namespace DiffBackup
{
    [ApiVersion(2, 1)]
    public class DiffBackupPlugin : TerrariaPlugin
    {
        public const string Command = "tdiff";

        private readonly IBackupService _backupService;

        private readonly FileSystemWatcher? _watcher;
        public readonly WorldSaveTrackingStrategy TrackingStrategy = WorldSaveTrackingStrategy.SaveEventListener;
        private Stopwatch _lastWriteStopwatch = new Stopwatch();

        private DateTime? _prevRestoreDate;
        private Stopwatch _prevRestoreStopwatch = new Stopwatch();
        private string? _prevUserUuid;

        /// <summary>
        ///     Initializes a new instance of the TestPlugin class.
        ///     This is where you set the plugin's order and perfrom other constructor logic
        /// </summary>
        public DiffBackupPlugin(Main game) : base(game)
        {
            Order = 10;
            if (TrackingStrategy.HasFlag(WorldSaveTrackingStrategy.FileSystemWatcher))
            {
                _watcher = new FileSystemWatcher {IncludeSubdirectories = false};
            }

            if (TrackingStrategy == WorldSaveTrackingStrategy.None)
            {
                this.LogWarn("Tracking strategy set to none.");
            }

            _backupService = new BackupService(this.Logger());
        }

        /// <summary>
        ///     Gets the author(s) of this plugin
        /// </summary>
        public override string Author => "Tieonlinux";

        /// <summary>
        ///     Gets the description of this plugin.
        ///     A short, one lined description that tells people what your plugin does.
        /// </summary>
        public override string Description => "World file backup system";

        /// <summary>
        ///     Gets the name of this plugin.
        /// </summary>
        public override string Name => "TDiffBackup";

        /// <summary>
        ///     Gets the version of this plugin.
        /// </summary>
        public override Version Version => GetType().Assembly.GetName().Version;

        /// <summary>
        ///     Handles plugin initialization.
        ///     Fired when the server is started and the plugin is being loaded.
        ///     You may register hooks, perform loading procedures etc here.
        /// </summary>
        public override void Initialize()
        {
            this.LogDebug("Initialize");
            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            ServerApi.Hooks.GamePostInitialize.Register(this, OnPostInitialize);

            if (TrackingStrategy.HasFlag(WorldSaveTrackingStrategy.SaveEventListener))
            {
                ServerApi.Hooks.WorldSave.Register(this, OnWorldSaved);
            }
        }


        /// <summary>
        ///     Handles plugin disposal logic.
        ///     *Supposed* to fire when the server shuts down.
        ///     You should deregister hooks and free all resources here.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.LogDebug("Disposing");

                this.LogDebug("Removing all hooks");
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                ServerApi.Hooks.GameInitialize.Deregister(this, OnPostInitialize);
                if (TrackingStrategy.HasFlag(WorldSaveTrackingStrategy.SaveEventListener))
                {
                    ServerApi.Hooks.WorldSave.Deregister(this, OnWorldSaved);
                }

                _backupService.Dispose();
                _watcher?.Dispose();
            }

            base.Dispose(disposing);
        }


        private void OnInitialize(EventArgs args)
        {
            this.LogDebug("Adding chat commands");
            Commands.ChatCommands.Add(new Command(Command, DiffCmd, Command));
        }

        private void DiffCmd(CommandArgs args)
        {
            if (args.Parameters.Count == 0 || args.Parameters[0].ToLowerInvariant() == "help")
            {
                args.Player.SendInfoMessage($"/{Command} ls <date> - list backups");
                args.Player.SendInfoMessage($"/{Command} <date> - restore closest backup @ date");
                args.Player.SendInfoMessage($"/{Command} cleanup - to remove old backup entries");
                return;
            }

            bool TryParseDate(out DateTime dateTime, int shift = 0)
            {
                var formats = new[] {"yyyy/MM/dd HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "yyyy/MM/dd"};
                if (args.Parameters.Count > shift)
                {
                    var str = string.Join(" ", args.Parameters.GetRange(shift, args.Parameters.Count - shift)).Trim();
                    foreach (var format in formats)
                    {
                        try
                        {
                            dateTime = DateTime.ParseExact(str, format, CultureInfo.InvariantCulture);
                            this.LogDebug($"got valid datetime {dateTime} for {str}");
                            return true;
                        }
                        catch (FormatException e)
                        {
                            this.LogDebug(e.BuildExceptionString());
                            this.LogDebug(str);
                        }
                    }

                    dateTime = DateParser.Parse(str);
                    return true;
                }

                dateTime = DateTime.Now;
                return false;
            }

            var date = DateTime.Now;

            List<DateTime> ListAndSortBackupDatetime()
            {
                var res = _backupService.ListBackup(Main.worldPathName, CancellationToken.None);

                res.Sort((left, right) =>
                    (int) (Math.Abs((left - date).TotalSeconds) - Math.Abs((right - date).TotalSeconds)));

                return res;
            }

            if (args.Parameters.Count > 0)
            {
                if (args.Parameters[0].ToLowerInvariant() == "ls" || args.Parameters[0].ToLowerInvariant() == "list")
                {
                    TryParseDate(out date, 1);

                    DisplayBackups(args, date, ListAndSortBackupDatetime());
                    return;
                }

                if (args.Parameters[0].ToLowerInvariant() == "cleanup")
                {
                    if (TShock.ShuttingDown)
                    {
                        this.LogWarn("TShock is Shutting Down.");
                        return;
                    }

                    Task.Factory.StartNew(async () =>
                    {

                        await _backupService.StartCleanup(Main.WorldPath).ContinueWith(task =>
                        {
                            if (task.IsFaulted)
                            {
                                Exception exception = task.Exception;
                                if (exception is AggregateException aggex && aggex.InnerExceptions.Count == 1)
                                {
                                    exception = aggex.InnerExceptions.First();
                                }
                                args.Player.SendErrorMessage($"Error: {exception.Message}");
                                this.LogError(exception.BuildExceptionString());
                            }
                            else if (task.IsCompleted)
                            {
                                args.Player.SendSuccessMessage($"Deleted {task.Result.Count} old files");
                                this.LogInfo($"Backup cleanup done with {task.Result.Count} files deleted");
                            }
                        });
                    });
                    return;
                }

                if (args.Parameters[0].ToLowerInvariant() == "confirm")
                {
                    if (_prevRestoreDate is null || !_prevRestoreStopwatch.IsRunning)
                    {
                        args.Player.SendErrorMessage("There's nothing to confirm for now");
                        return;
                    }

                    if (_prevUserUuid != args.Player.UUID)
                    {
                        args.Player.SendErrorMessage("Someone else is trying to restore a backup !");
                        return;
                    }

                    if (_prevRestoreStopwatch.Elapsed > TimeSpan.FromMinutes(2))
                    {
                        args.Player.SendErrorMessage("Confirmation timeout ! Try again");
                        return;
                    }

                    date = _prevRestoreDate.Value;
                }
                else if (!TryParseDate(out date) || date == default)
                {
                    args.Player.SendErrorMessage("Unable to parse given date");
                    return;
                }
            }

            var dates = ListAndSortBackupDatetime();
            if (!dates.Any())
            {
                args.Player.SendErrorMessage("No backup for now.");
                return;
            }

            if (Math.Abs((dates[0] - date).TotalSeconds) < 1)
            {
                args.Player.SendWarningMessage($"Restoring backup @ {dates[0]}. Server is about to stop.");
                this.LogWarn($"Restoring backup @ {dates[0]}");
                try
                {
                    _backupService.Restore(Main.worldPathName, dates[0], CancellationToken.None);
                }
                catch (Exception e)
                {
                    this.LogDebug(e.BuildExceptionString(), TraceLevel.Error);
                    throw;
                }

                TShock.Utils.StopServer(false, "Restoring backup");
            }
            else
            {
                args.Player.SendWarningMessage(
                    $"In order to restore backup @ {dates[0]}, you need to enter /{Command} confirm");
                _prevRestoreDate = dates[0];
                _prevRestoreStopwatch = Stopwatch.StartNew();
                _prevUserUuid = args.Player.UUID;
            }
        }

        private static bool DisplayBackups(CommandArgs args, DateTime date, List<DateTime> dates)
        {
            if (!dates.Any())
            {
                args.Player.SendErrorMessage("No backup for now.");
                return false;
            }

            dates = dates.GetRange(0, Math.Min(24, dates.Count));
            dates.Sort();
            var i = 0;
            var msg = "";
            while (i < dates.Count)
            {
                var token = dates[i].ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture);
                if (msg.Length + token.Length + 3 > 80)
                {
                    args.Player.SendInfoMessage(msg);
                    msg = "";
                }

                msg += " | " + token;
                i += 1;
            }

            if (msg.Length > 0)
            {
                args.Player.SendInfoMessage(msg);
            }

            return true;
        }

        private void OnPostInitialize(EventArgs args)
        {
            StartWatcher();
        }

        private void StartWatcher()
        {
            if (_watcher is null)
            {
                return;
            }

            _watcher.NotifyFilter |= NotifyFilters.LastWrite;
            _watcher.Filter = Path.GetFileName(Main.worldPathName);
            _watcher.Path = Path.GetDirectoryName(Main.worldPathName);
            _watcher.Changed += OnFileChanged;
            _watcher.EnableRaisingEvents = true;
            this.LogDebug("Started Watcher");
        }

        private void OnWorldSaved(EventArgs args)
        {
            OnFileChanged(this,
                new FileSystemEventArgs(WatcherChangeTypes.Changed, Path.GetDirectoryName(Main.worldPathName),
                    Path.GetFileName(Main.worldPathName)));
        }

        private void OnFileChanged(object source, FileSystemEventArgs e)
        {
            if (TShock.ShuttingDown)
            {
                this.LogDebug("TShock is Shutting Down");
                return;
            }

            this.LogDebug("OnFileChanged " + e.FullPath);
            var debounceDelay = TimeSpan.FromSeconds(5);
            var precisionCorrection = TimeSpan.FromSeconds(1.0 / 30);
            _lastWriteStopwatch = Stopwatch.StartNew();
            var now = DateTime.Now;

            async Task RecheckLaterAsync()
            {
                await Task.Delay(debounceDelay);
                if (TShock.ShuttingDown)
                {
                    this.LogWarn("TShock is Shutting Down.");
                    return;
                }

                if (_lastWriteStopwatch.Elapsed < debounceDelay - precisionCorrection)
                {
                    return;
                }

                try
                {
                    //try opening the file to check lock if any
                    using var _ = File.OpenRead(e.FullPath);
                }
                catch (IOException ex)
                {
                    this.LogWarn($"Unable to open world file {e.Name}.");
                    this.LogDebug(ex.BuildExceptionString(), TraceLevel.Error);
                    return;
                }

                await _backupService.StartBackup(e.FullPath, now);
            }

            Task.Factory.StartNew(async () => await RecheckLaterAsync());
        }
    }
}