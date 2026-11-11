namespace Kaon.Config;

public interface IConfig
{
    Task OnProcessing(ConfigFactory factory);
}

public interface IConfigBackend
{
    void Init(ConfigSerializerRegistry serializers) {}
    Dictionary<string, string> ReadAll(string path);
    bool WriteAll(string path, Dictionary<string, string> items);
}