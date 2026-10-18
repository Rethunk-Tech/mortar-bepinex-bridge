using System.Text.RegularExpressions;

namespace MortarBepInExBridge.Overlay;

/// <summary>What Mortar's stream overlay settings ask of this launch, read from <see cref="File"/> in the profile folder
/// that holds BepInEx's. Mortar writes the same PascalCase keys as the SMAPI bridge's config.json. Unity's Mono ships no
/// JSON parser, so the three known keys are read with patterns; Mortar's token is hex.</summary>
internal sealed class OverlayConfig(int port, string token)
{
    public int Port { get; } = port;
    public string Token { get; } = token;

    public const string File = "startup/overlay.json";
    internal const int MinTokenLength = 32;

    private static readonly Regex EnabledKey = new("\"OverlayEnabled\"\\s*:\\s*(true|false)", RegexOptions.Compiled);
    private static readonly Regex PortKey = new("\"OverlayPort\"\\s*:\\s*(\\d+)", RegexOptions.Compiled);
    private static readonly Regex TokenKey = new("\"OverlayToken\"\\s*:\\s*\"([A-Za-z0-9_-]+)\"", RegexOptions.Compiled);

    /// <summary>The overlay is on only when the file asks for it with a valid port and a token.</summary>
    public static OverlayConfig? Parse(string? json)
    {
        if (json == null)
            return null;
        Match enabled = EnabledKey.Match(json);
        Match port = PortKey.Match(json);
        Match token = TokenKey.Match(json);
        if (!enabled.Success || enabled.Groups[1].Value != "true" || !port.Success || !token.Success)
            return null;
        if (!int.TryParse(port.Groups[1].Value, out int number) || number is < 1 or > 65535)
            return null;
        if (token.Groups[1].Value.Length < MinTokenLength)
            return null;
        return new OverlayConfig(number, token.Groups[1].Value);
    }

    /// <summary>Why a file that asks for overlay cannot start it. Null when overlay should stay off without a
    /// warning: no file, OverlayEnabled not true, or the file is valid.</summary>
    public static string? InvalidWhenEnabled(string? json)
    {
        if (json == null)
            return null;
        Match enabled = EnabledKey.Match(json);
        if (!enabled.Success || enabled.Groups[1].Value != "true")
            return null;
        if (Parse(json) != null)
            return null;
        Match port = PortKey.Match(json);
        Match token = TokenKey.Match(json);
        if (!port.Success || !token.Success)
            return "overlay.json is enabled but OverlayPort or OverlayToken is missing";
        if (!int.TryParse(port.Groups[1].Value, out int number) || number is < 1 or > 65535)
            return "overlay.json OverlayPort is not a TCP port";
        return "overlay.json OverlayToken is too short";
    }
}
