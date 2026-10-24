using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace MortarBepInExBridge;

// Starts each message logged through Unity's logger with where it came from: the calling method with the IL offset of
// the call, and the hierarchy path of the object the call named. Text alone cannot tell apart the game's many bare "1"
// lines; the call site can, and Mortar takes the tag off the line to show it and to hide one source.
internal sealed class LogSources : ILogHandler
{
    private static readonly Assembly Own = typeof(LogSources).Assembly;
    private readonly ILogHandler inner;
    private readonly Dictionary<string, List<(string Text, string Object)>> hidden;

    private LogSources(ILogHandler inner, Dictionary<string, List<(string Text, string Object)>> hidden)
    {
        this.inner = inner;
        this.hidden = hidden;
    }

    // Install wraps Unity's logger; hidden is the sources Mortar asked this launch to drop (LogTag.ParseHidden).
    internal static void Install(string? hidden)
    {
        ILogger logger = Debug.unityLogger;
        if (logger.logHandler is not LogSources)
            logger.logHandler = new LogSources(logger.logHandler, LogTag.ParseHidden(hidden));
    }

    public void LogFormat(LogType logType, Object? context, string format, params object[] args)
    {
        string? site = LogTag.CallSite(new StackTrace(1, false), IsLogger);
        string? path = PathOf(context);
        if (LogTag.Hides(this.hidden, site, () => Format(format, args), path))
            return;
        string tag = LogTag.Of(site, path);
        this.inner.LogFormat(logType, context, tag.Replace("{", "{{").Replace("}", "}}") + format, args);
    }

    public void LogException(Exception exception, Object? context) => this.inner.LogException(exception, context);

    private static string Format(string format, object[] args)
    {
        try
        {
            return args.Length == 0 ? format : string.Format(format, args);
        }
        catch (FormatException)
        {
            return format;
        }
    }

    // The logging code is this plugin and Unity's engine modules (UnityEngine.CoreModule, AudioModule, ...), so a
    // warning Unity logs for its caller (PlayOneShot with no clip) names the game method that called it.
    private static bool IsLogger(MethodBase m)
    {
        Assembly? a = m.DeclaringType?.Assembly;
        if (a == null)
            return false;
        string name = a.GetName().Name ?? "";
        return a == Own || name == "UnityEngine" || (name.StartsWith("UnityEngine.", StringComparison.Ordinal) && name.EndsWith("Module", StringComparison.Ordinal));
    }

    // PathOf is the hierarchy path of the object a log call named, from the scene root; Unity objects are read only on
    // the main thread, which is where a call that passes one runs.
    private static string? PathOf(Object? context)
    {
        try
        {
            Transform? t = context switch
            {
                GameObject g => g.transform,
                Component c => c.transform,
                _ => null,
            };
            if (t == null)
                return context == null ? null : context.name;
            string path = t.name;
            for (Transform? p = t.parent; p != null; p = p.parent)
                path = p.name + "/" + path;
            return path;
        }
        catch (UnityException)
        {
            return null;
        }
    }
}
