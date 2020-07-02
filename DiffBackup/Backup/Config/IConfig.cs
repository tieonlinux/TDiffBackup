using Topten.JsonKit_tie_tdiff;

namespace DiffBackup.Backup.Config
{
    public interface IConfig
    {
        IConfig Clone();
        T Clone<T>() where T : IConfig;
        string ToJson(JsonOptions options = JsonOptions.None);

        void SetFromJson(string payload, JsonOptions options = JsonOptions.None);
    }
}