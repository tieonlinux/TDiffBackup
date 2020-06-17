using System;

namespace DiffBackup
{
    [Flags]
    public enum WorldSaveTrackingStrategy : byte
    {
        None,
        FileSystemWatcher,
        SaveEventListener,
        All = FileSystemWatcher | SaveEventListener
    }
}