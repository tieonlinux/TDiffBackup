using System.IO;
using System.Text;
using Topten.JsonKit_tie_tdiff;
using TShockAPI;

#nullable enable
namespace DiffBackup.Backup.Config.Json
{
    public class TShockConfigHandler
    {
        public readonly string BasePath;

        public TShockConfigHandler(string basePath)
        {
            JsonConverters.Install();
            BasePath = Path.GetFullPath(basePath);
        }

        public TShockConfigHandler() : this(TShock.SavePath)
        {
        }


        public T LoadJson<T>(string fileName) where T : class?, new()
        {
            var path = Path.Combine(BasePath, fileName);
            var content = File.ReadAllText(path, Encoding.UTF8);
            return Topten.JsonKit_tie_tdiff.Json.Parse<T>(content, JsonOptions.NonStrictParser);
        }

        public bool TryLoadJson<T>(string fileName, out T result)
            where T : new()
        {
            var path = GetFullPath(fileName);

            if (!File.Exists(path))
            {
                result = new T();
                return false;
            }

            var content = File.ReadAllText(path, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(content))
            {
                result = new T();
                return false;
            }

            result = Topten.JsonKit_tie_tdiff.Json.Parse<T>(content, JsonOptions.NonStrictParser);
            return true;
        }

        public void SaveConfig<T>(T config, string fileName)
        {
            var path = GetFullPath(fileName);
            var payload = Topten.JsonKit_tie_tdiff.Json.Format(config);
            File.WriteAllText(path, payload, Encoding.UTF8);
        }

        public string GetFullPath(string fileName)
        {
            return Path.Combine(BasePath, fileName);
        }
    }
}