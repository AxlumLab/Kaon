using System.Text;

namespace Kaon.Protocol;

public static class StringFrameCodec
{
    public static byte[] Encode(string value)
    {
        return Encoding.UTF8.GetBytes(value);
    }

    public static string Decode(Frame frame)
    {
        return Encoding.UTF8.GetString(frame.Payload.Span);
    }
}
