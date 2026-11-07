using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;

namespace MortarBepInExBridge;

/// <summary>
/// Unity's Mono cannot always build the code base of an assembly it loaded from a non-ASCII game folder: the native
/// get_code_base throws "String conversion error: Illegal byte sequence" for every game assembly, so Assembly.GetName(),
/// which MonoMod calls on a hook target's assembly, fails for any mod that hooks game code. Location of the same
/// assembly reads fine, so the code base is rebuilt from it. Harmony is bound by reflection for the reason given on
/// <see cref="PatchTiming"/>.
/// </summary>
internal static class CodeBaseFix
{
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    /// <summary>False when this Harmony or mscorlib is not the one the fix binds to.</summary>
    public static bool Install()
    {
        Type? harmony = Type.GetType("HarmonyLib.Harmony, 0Harmony");
        Type? method = Type.GetType("HarmonyLib.HarmonyMethod, 0Harmony");
        MethodInfo? target = typeof(Assembly).Assembly.GetType("System.Reflection.RuntimeAssembly")?
            .GetMethod("GetCodeBase", Any, null, [typeof(Assembly), typeof(bool)], null);
        if (harmony == null || method == null || target == null)
            return false;
        MethodInfo patch = harmony.GetMethods().First(m => m.Name == "Patch" && m.GetParameters().Length >= 2 && m.GetParameters()[0].ParameterType == typeof(MethodBase));
        ParameterInfo[] parameters = patch.GetParameters();
        int finalizer = Array.FindIndex(parameters, p => p.Name == "finalizer");
        if (finalizer < 0)
            return false;
        object?[] args = new object?[parameters.Length];
        args[0] = target;
        args[finalizer] = Activator.CreateInstance(method, typeof(CodeBaseFix).GetMethod(nameof(Finalizer), Any))!;
        patch.Invoke(Activator.CreateInstance(harmony, "Rethunk.MortarBepInExBridge.codebase"), args);
        return true;
    }

    /// <summary>The code base Mono would report, rebuilt from the assembly's location; null when there is none.</summary>
    internal static string? FromLocation(string? location, bool escaped)
    {
        if (string.IsNullOrEmpty(location) || !Uri.TryCreate(location, UriKind.Absolute, out Uri? uri))
            return null;
        return escaped ? uri.AbsoluteUri : uri.ToString();
    }

    /// <summary>
    /// The code base for an assembly whose get_code_base threw. Handled is false when nothing sane can be reported, so
    /// the original exception stands. A memory-loaded assembly (empty location, such as Unity's own modules) falls back
    /// to its file in the game's Managed folder, else to a null code base, which AssemblyName accepts.
    /// </summary>
    internal static (bool Handled, string? CodeBase) Rebuild(string? location, string? fullName, string? managedDir, Func<string, bool> exists, bool escaped)
    {
        if (!string.IsNullOrEmpty(location))
        {
            string? fromLocation = FromLocation(location, escaped);
            return (fromLocation != null, fromLocation);
        }
        // FullName reads fine where the native conversion fails; the simple name ends at the first comma.
        string simple = (fullName ?? "").Split(',')[0].Trim();
        if (!string.IsNullOrEmpty(managedDir) && simple.Length > 0 && simple.IndexOfAny(['/', '\\']) < 0)
        {
            string file = managedDir.TrimEnd('/', '\\') + "/" + simple + ".dll";
            if (exists(file))
                return (true, FromLocation(file, escaped));
        }
        return (true, null);
    }

    // Returning the exception keeps every other failure as it was.
    private static Exception? Finalizer(Exception? __exception, ref string? __result, Assembly a, bool escaped)
    {
        if (__exception == null)
            return null;
        (bool handled, string? codeBase) result;
        try
        {
            result = Rebuild(a.Location, a.FullName, Paths.ManagedPath, File.Exists, escaped);
        }
        catch (Exception)
        {
            return __exception;
        }
        if (!result.handled)
            return __exception;
        __result = result.codeBase;
        return null;
    }
}
