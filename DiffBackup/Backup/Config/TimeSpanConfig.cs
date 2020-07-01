using System;
using DiffBackup.Backup.Config.Json;

namespace DiffBackup.Backup.Config
{
    public readonly struct TimeSpanConfig
    {
        public readonly TimeSpan Value;

        public TimeSpanConfig(TimeSpan value)
        {
            Value = value;
        }

        public static implicit operator TimeSpan(TimeSpanConfig input)
        {
            return input.Value;
        }

        public static implicit operator TimeSpanConfig(TimeSpan input)
        {
            return new TimeSpanConfig(input);
        }

        public string FormatJson()
        {
            return $"{Value.ToString(JsonConverters.TimeSpanFormatString)}";
        }

        public static TimeSpanConfig ParseJson(string literal)
        {
            return TimeSpan.ParseExact(literal, JsonConverters.TimeSpanFormatString, null);
        }
    }
}