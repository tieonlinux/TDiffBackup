namespace DiffBackup.Backup.Config
{
    public class InternalConfig
    {
        public double ThrottleSecond { get; set; } = 60;
    }


    public class StrategyConfig
    {
        public double EveryXDayDoFullBackup { get; set; } = 7;
        public double FillFactorToStartFullBackup { get; set; } = 0.5;
    }

    public class Config
    {
        public StrategyConfig Strategy { get; set; } = new StrategyConfig();
        public InternalConfig Internal { get; set; } = new InternalConfig();
    }
}