using System;
using Topten.JsonKit_tie_tdiff;

namespace DiffBackup.Backup.Config.Json
{
    public static class JsonConverters
    {
        /// <summary>
        ///     Format: Days.Hours:Minutes:Seconds:Milliseconds
        /// </summary>
        public const string TimeSpanFormatString = @"d\.hh\:mm\:ss\:FFF";

        private static readonly object Lock = new object();

        static JsonConverters()
        {
            Install();
        }

        public static bool Installed { get; private set; }

        public static bool Install()
        {
            if (!Installed)
            {
                lock (Lock)
                {
                    if (!Installed)
                    {
                        Topten.JsonKit_tie_tdiff.Json.RegisterFormatter<TimeSpanConfig>(WriteJson);
                        Topten.JsonKit_tie_tdiff.Json.RegisterParser(TimeSpanParseJson);
                        Topten.JsonKit_tie_tdiff.Json.RegisterFormatter<WorldSaveTrackingStrategy>(WriteJson);
                        Topten.JsonKit_tie_tdiff.Json.RegisterParser(WorldSaveTrackingStrategyParseJson);
                        Installed = true;
                        return true;
                    }
                }
            }

            return false;
        }

        private static TimeSpanConfig TimeSpanParseJson(object literal)
        {
            var res = TimeSpan.ParseExact((string) literal, TimeSpanFormatString, null);
            return res;
        }

        private static void WriteJson(IJsonWriter writer, TimeSpanConfig value)
        {
            writer.WriteStringLiteral($"{value.Value.ToString(TimeSpanFormatString)}");
        }

        private static void WriteJson(IJsonWriter writer, WorldSaveTrackingStrategy value)
        {
            writer.WriteStringLiteral(value.ToString());
        }

        private static WorldSaveTrackingStrategy WorldSaveTrackingStrategyParseJson(object literal)
        {
            if (Enum.TryParse((string) literal, out WorldSaveTrackingStrategy res))
            {
                return res;
            }

            return WorldSaveTrackingStrategy.SaveEventListener;
        }
    }
}