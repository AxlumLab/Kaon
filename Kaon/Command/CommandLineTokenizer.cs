using System.Text;

namespace Kaon.Command;

public static class CommandLineTokenizer
{
    public static IReadOnlyList<string> Tokenize(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var quote = '\0';
        var escaping = false;

        foreach (var ch in line)
        {
            if (escaping)
            {
                current.Append(ch);
                escaping = false;
                continue;
            }

            if (ch == '\\')
            {
                escaping = true;
                continue;
            }

            if (quote != '\0')
            {
                if (ch == quote)
                    quote = '\0';
                else
                    current.Append(ch);
                continue;
            }

            if (ch is '"' or '\'')
            {
                quote = ch;
                continue;
            }

            if (char.IsWhiteSpace(ch))
            {
                Flush(result, current);
                continue;
            }

            current.Append(ch);
        }

        Flush(result, current);
        return result;
    }

    private static void Flush(List<string> result, StringBuilder current)
    {
        if (current.Length == 0)
            return;

        result.Add(current.ToString());
        current.Clear();
    }
}
