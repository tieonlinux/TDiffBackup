namespace DiffBackup.Backup.Config
{
    public class AllConfig : BaseConfig
    {
        public int Version { get; set; } = 1;
        public StrategyConfig Strategy { get; set; } = new StrategyConfig();
        public InternalConfig Internal { get; set; } = new InternalConfig();
    }
}