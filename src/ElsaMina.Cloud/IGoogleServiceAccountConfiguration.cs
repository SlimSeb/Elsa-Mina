namespace ElsaMina.Cloud;

public interface IGoogleServiceAccountConfiguration
{
    /// <summary>
    /// The fields of the Google service account key file, as found in config.json.
    /// </summary>
    IReadOnlyDictionary<string, string> GoogleServiceAccountData { get; }
}
