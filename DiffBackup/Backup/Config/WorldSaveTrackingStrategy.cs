using System;

namespace DiffBackup
{
    [Flags]
    public enum WorldSaveTrackingStrategy : byte
    {
        None = 0,
        FileSystemWatcher = 1,
        SaveEventListener = 1 << 1,
        All = FileSystemWatcher | SaveEventListener,
        Default = SaveEventListener
    }
}