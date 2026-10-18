using System;
using System.Reflection;

namespace MortarBepInExBridge.Overlay;

/// <summary>Reads a game's objects by member name, so a provider names the game's types as strings and the plugin
/// references no game assembly. Every read answers null when a type, member or value is missing: a game update that
/// renames something blanks a field instead of throwing.</summary>
internal static class Probe
{
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    public static object? Static(Type? type, string name) => type == null ? null : Read(type, null, name);

    public static object? Member(object? target, string name) => target == null ? null : Read(target.GetType(), target, name);

    /// <summary>The result of a parameterless method, or null.</summary>
    public static object? Call(object? target, string name)
    {
        if (target == null)
            return null;
        MethodInfo? method = target.GetType().GetMethod(name, Any, null, Type.EmptyTypes, null);
        return method?.Invoke(method.IsStatic ? null : target, null);
    }

    public static int? Int(object? value) => value switch
    {
        int i => i,
        uint u => (int)u,
        long l => (int)l,
        short s => s,
        ushort us => us,
        byte b => b,
        sbyte sb => sb,
        float f => (int)f,
        double d => (int)d,
        _ => null,
    };

    private static object? Read(Type type, object? target, string name)
    {
        for (Type? t = type; t != null; t = t.BaseType)
        {
            FieldInfo? field = t.GetField(name, Any | BindingFlags.DeclaredOnly);
            if (field != null)
                return field.GetValue(field.IsStatic ? null : target);
            PropertyInfo? property = t.GetProperty(name, Any | BindingFlags.DeclaredOnly);
            if (property != null && property.GetIndexParameters().Length == 0 && property.CanRead)
                return property.GetValue(property.GetGetMethod(true)!.IsStatic ? null : target);
        }
        return null;
    }
}
