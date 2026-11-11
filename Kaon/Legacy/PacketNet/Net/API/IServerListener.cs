namespace NetStickShared.Net.API;

public interface IServerListener
{
    ISocketListener PreConnect();
    void OnStop();
    void OnActive();
    void OnDead();
}