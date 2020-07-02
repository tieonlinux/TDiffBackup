using System;
using Topten.JsonKit_tie_tdiff;

namespace DiffBackup.Backup.Config
{
    public abstract class BaseConfig : IConfig
    {
        public BaseConfig()
        {
        }

        public BaseConfig(BaseConfig copy)
        {
            Topten.JsonKit_tie_tdiff.Json.CloneInto(this, copy);
        }

        public IConfig Clone()
        {
            return Clone<IConfig>();
        }

        public T Clone<T>() where T : IConfig
        {
            if (this is T t)
            {
                return Topten.JsonKit_tie_tdiff.Json.Clone(t);
            }

            throw new TypeAccessException();
        }

        public string ToJson(JsonOptions options = JsonOptions.None)
        {
            return Topten.JsonKit_tie_tdiff.Json.Format(this, options);
        }

        public void SetFromJson(string data, JsonOptions options = JsonOptions.None)
        {
            Topten.JsonKit_tie_tdiff.Json.ParseInto(data, this, options);
        }
    }
}