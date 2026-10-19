using MortarBepInExBridge.Games.LethalCompany;

namespace MortarBepInExBridge;

internal static partial class BuiltGame
{
    static partial void Register(ref IGameHooks? hooks) => hooks = new LethalCompanyHooks();
}
