using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace MortarBepInExBridge;

// The tag LogSources starts a message with. No Unity types here, so tests load it without the game.
internal static class LogTag
{
    internal const string Open = "\u27e6";
    internal const string Close = "\u27e7";

    // CallSite is the first frame outside the logging code, as Type::Method@offset (hex IL offset of the call).
    internal static string? CallSite(StackTrace trace, Func<MethodBase, bool> skip)
    {
        foreach (StackFrame frame in trace.GetFrames() ?? [])
        {
            MethodBase? method = frame.GetMethod();
            Type? type = method?.DeclaringType;
            if (method == null || type == null || skip(method))
                continue;
            return $"{type.FullName}::{method.Name}@{frame.GetILOffset():x}";
        }
        return null;
    }

    internal static string Of(string? site, string? path)
    {
        if (site == null && path == null)
            return "";
        var b = new StringBuilder(Open);
        if (site != null)
            b.Append("src=").Append(Clean(site));
        if (path != null)
            b.Append(site == null ? "" : ";").Append("obj=").Append(Clean(path));
        return b.Append(Close).Append(' ').ToString();
    }

    private static string Clean(string s) => s.Replace(Close, "").Replace(";", ",").Replace("\n", " ");

    // ParseHidden reads Mortar's list of hidden sources, one "source<TAB>object" per line (empty object: every object).
    internal static HashSet<string> ParseHidden(string? text)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (string line in (text ?? "").Split('\n'))
        {
            string l = line.TrimEnd('\r');
            if (l.Contains("::"))
                set.Add(l.Contains('\t') ? l : l + "\t");
        }
        return set;
    }

    internal static bool Hides(HashSet<string> hidden, string? site, string? path) =>
        site != null && hidden.Count > 0 && (hidden.Contains(site + "\t") || (path != null && hidden.Contains(site + "\t" + path)));
}
