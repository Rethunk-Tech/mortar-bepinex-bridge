using System.Text;

namespace MortarBepInExBridge;

/// <summary>Just enough JSON to answer a command on one line; Unity's Mono profile ships no JSON library.</summary>
internal static class Json
{
    public static string Quote(string? value)
    {
        var sb = new StringBuilder("\"");
        foreach (char c in value ?? "")
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case < ' ': sb.Append("\\u").Append(((int)c).ToString("x4")); break;
                default: sb.Append(c); break;
            }
        }
        return sb.Append('"').ToString();
    }
}
