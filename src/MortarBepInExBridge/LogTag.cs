using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace MortarBepInExBridge;

// The tag LogSources starts a message with. No Unity types here, so tests load it without the game.
internal static class LogTag
{
    internal const string Open = "\u27e6";
    internal const string Close = "\u27e7";

    // CallSite is the first frame outside the logging code, as Type::Method. A release Mono has no IL offsets (every
    // frame reads 0), so the masked text tells one method's calls apart instead.
    internal static string? CallSite(StackTrace trace, Func<MethodBase, bool> skip)
    {
        foreach (StackFrame frame in trace.GetFrames() ?? [])
        {
            MethodBase? method = frame.GetMethod();
            Type? type = method?.DeclaringType;
            if (method == null || type == null || skip(method))
                continue;
            return $"{type.FullName}::{method.Name}";
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

    private static readonly Regex Number = new("-?[0-9]+(?:[.,:][0-9]+)*", RegexOptions.CultureInvariant);

    // Mask is a message's first line with every number as "#", the same as Mortar's Console masks it.
    internal static string Mask(string message)
    {
        int end = message.IndexOf('\n');
        string first = end < 0 ? message : message[..end];
        return Number.Replace(first.TrimEnd('\r'), "#").Trim();
    }

    // ParseHidden reads Mortar's list of hidden sources, one "method<TAB>text<TAB>object" per line: the method's lines
    // whose masked text matches (empty: any), about that object (empty: any).
    internal static Dictionary<string, List<(string Text, string Object)>> ParseHidden(string? text)
    {
        var hidden = new Dictionary<string, List<(string, string)>>(StringComparer.Ordinal);
        foreach (string line in (text ?? "").Split('\n'))
        {
            string[] f = line.TrimEnd('\r').Split('\t');
            if (!f[0].Contains("::"))
                continue;
            if (!hidden.TryGetValue(f[0], out var list))
                hidden[f[0]] = list = [];
            list.Add((f.Length > 1 ? f[1] : "", f.Length > 2 ? f[2] : ""));
        }
        return hidden;
    }

    // Hides formats the message only for a method something is hidden from, since most lines come from others.
    internal static bool Hides(Dictionary<string, List<(string Text, string Object)>> hidden, string? site, Func<string> message, string? path)
    {
        if (site == null || !hidden.TryGetValue(site, out var list))
            return false;
        string masked = Mask(message());
        return list.Exists(h => (h.Text == "" || h.Text == masked) && (h.Object == "" || h.Object == path));
    }
}
