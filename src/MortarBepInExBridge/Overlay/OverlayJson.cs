using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MortarBepInExBridge.Overlay;

/// <summary>Writes a snapshot as one JSON object. Null values are left out, so a reader sees a missing key, not a made-up zero.</summary>
internal static class OverlayJson
{
    public static string Write(IReadOnlyDictionary<string, object?> values)
    {
        var sb = new StringBuilder("{");
        bool first = true;
        foreach (KeyValuePair<string, object?> pair in values)
        {
            if (pair.Value == null)
                continue;
            if (!first)
                sb.Append(',');
            first = false;
            sb.Append(Json.Quote(pair.Key)).Append(':');
            Append(sb, pair.Value);
        }
        return sb.Append('}').ToString();
    }

    private static void Append(StringBuilder sb, object value)
    {
        switch (value)
        {
            case bool b: sb.Append(b ? "true" : "false"); break;
            case string s: sb.Append(Json.Quote(s)); break;
            case int or long or short: sb.Append(System.Convert.ToString(value, CultureInfo.InvariantCulture)); break;
            case double d: sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); break;
            case IEnumerable list:
                sb.Append('[');
                bool first = true;
                foreach (object? item in list)
                {
                    if (item == null)
                        continue;
                    if (!first)
                        sb.Append(',');
                    first = false;
                    Append(sb, item);
                }
                sb.Append(']');
                break;
            default: sb.Append(Json.Quote(value.ToString())); break;
        }
    }
}
