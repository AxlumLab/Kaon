namespace Kaon.Config;

public interface IGeneratedConfigMetadata
{
    string? ConfigFileName { get; }
    int ConfigVersion { get; }
    IConfigBackend? CreateBackend();
    void RegisterSerializers(ConfigSerializerRegistry registry);
}
