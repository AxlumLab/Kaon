namespace Kaon.IO;

public interface IStreamable
{
    void OnSerialize(PackedStream ss);
    void OnDeSerialize(PackedStream ss);
}