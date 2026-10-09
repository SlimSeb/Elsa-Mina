namespace ElsaMina.Cloud;

public interface IGoogleServiceAccountConfiguration
{
    /// <summary>
    /// Les champs du fichier de clé du service account Google, copiés tels quels depuis config.json
    /// </summary>
    IReadOnlyDictionary<string, string> GoogleServiceAccountData { get; }
}
