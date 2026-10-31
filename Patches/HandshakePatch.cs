using Hazel;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using InnerNet;
using System;

namespace TONE.Patches;

[Flags]
public enum ModFlags : ushort
{

    None = 0,

    RequireOnAllClients = 1 << 0,

    RequireOnServer = 1 << 1,

    RequireOnHost = 1 << 2,

    DisableServerAuthority = 1 << 3,
}

public enum ReactorProtocolVersion : byte
{

    V2 = 1,

    V3 = 2,

    Latest = V3,
}

#if !ANDROID
[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.GetConnectionData))]
public static class HandshakePatch
{
    private const ulong MAGIC = 0x72656163746f72;

    public static void Prefix(ref bool useDtlsLayout)
    {

        try
        {
            var client = AmongUsClient.Instance;

            var original = useDtlsLayout;
            var fromClient = client?.useDtls;

            Logger.Info(
                $"GetConnectionData PREFIX: useDtlsLayout(原值)={original} "
                + $"AmongUsClient.Instance={(client == null ? "<null>" : "ok")} "
                + $"client.useDtls={(client == null ? "<n/a>" : fromClient?.ToString() ?? "<null>")}",
                "HandshakePatch");

            if (client == null) return;
            useDtlsLayout = client.useDtls;

            Logger.Info($"GetConnectionData PREFIX: useDtlsLayout(改写后)={useDtlsLayout}", "HandshakePatch");
        }
        catch (System.Exception ex)
        {
            Logger.Warn($"HandshakePatch.Prefix 跳过（已保护）：{ex}", "HandshakePatch");
        }
    }

    public static void Postfix(ref Il2CppStructArray<byte> __result, [HarmonyArgument(1)] string matchmakerToken)
    {

        var skip = IsLocalOrFreePlay() || string.IsNullOrEmpty(matchmakerToken);

        Logger.Info(
            $"GetConnectionData: token=\"{(matchmakerToken ?? "<null>")}\" "
            + $"IsLocal={SafeIsLocal()} IsFreePlay={SafeIsFreePlay()} skip={skip}",
            "HandshakePatch");

        if (skip) return;

        if (!Main.HasReactorPlugin)
        {
            var handshake = new MessageWriter(1000);

            handshake.Write(__result);

            var version = (byte)ReactorProtocolVersion.Latest;
            var value = (MAGIC << 8) | version;
            handshake.Write(value);

            handshake.WritePacked(1);
            handshake.Write(Main.PluginGuid);
            handshake.Write(Main.PluginDisplayVersion);
            handshake.Write((ushort)(ModFlags.RequireOnHost | ModFlags.DisableServerAuthority));

            __result = handshake.ToByteArray(true);
            handshake.Recycle();
        }
    }

    private static bool IsLocalOrFreePlay() => SafeIsLocal() || SafeIsFreePlay();

    private static bool SafeIsLocal()
    {
        try { return AmongUsClient.Instance != null && GameStates.IsLocalGame; }
        catch { return false; }
    }

    private static bool SafeIsFreePlay()
    {
        try { return AmongUsClient.Instance != null && GameStates.IsFreePlay; }
        catch { return false; }
    }
}
#endif