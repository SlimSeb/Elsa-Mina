namespace ElsaMina.DataAccess;

public interface IDatabaseConfiguration
{
    string ConnectionString { get; }
    int DatabaseMaxRetries { get; }
    TimeSpan DatabaseRetryDelay { get; }
}
