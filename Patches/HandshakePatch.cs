using Hazel;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using InnerNet;
using System;

namespace TONE.Patches;

// This is taken from Reactor modded handshake.
// We do not want to pull in Reactor as a dependency though.

// Code from: https://github.com/NuclearPowered/Reactor/blob/9bd370bfc10d666431efdb70f4cd02f0d9eefe91/Reactor/Networking/Patches/ClientPatches.cs#L321
// Credit: xtracube, miniduikboot, 6pak

/// <summary>
/// Represents flags of the mod.
/// </summary>
[Flags]
public enum ModFlags : ushort
{
    /// <summary>
    /// No flags.
    /// </summary>
    None = 0,

    /// <summary>
    /// Requires all clients in a lobby to have the mod.
    /// </summary>
    RequireOnAllClients = 1 << 0,

    /// <summary>
    /// Requires the server to have a plugin that handles the mod.
    /// </summary>
    RequireOnServer = 1 << 1,

    /// <summary>
    /// Requires the host of the lobby to have the mod.
    /// </summary>
    RequireOnHost = 1 << 2,

    /// <summary>
    /// Notifies the game server that the host has authority over game logic.
    /// </summary>
    DisableServerAuthority = 1 << 3,
}


/// <summary>
///     Version of the Reactor.Networking protocol format.
/// </summary>
public enum ReactorProtocolVersion : byte
{
    /// <summary>
    ///     First public Reactor Protocol version.
    /// </summary>
    V2 = 1,

    /// <summary>
    ///     Version introducing vanilla server support, syncer concept and registries.
    /// </summary>
    V3 = 2,

    /// <summary>
    ///     Latest version.
    /// </summary>
    Latest = V3,
}

// ⚠️ Brush Up Roles：本补丁**仅电脑端编译**（#if !ANDROID）。
//
//    与已发布的正式版一致：
//      · 电脑端：握手**启用**（官方行为，服务器/其它客户端可识别模组身份）
//      · 手机端：本文件整体不参与编译（正式版手机端 HandshakePatch 为 0）
//
//    实测背景：本补丁 Prefix 会改写 useDtlsLayout、Postfix 往连接数据尾部追加
//    Reactor 握手段。手机上曾出现：
//        Hosting a local game
//        → GetConnectionData 抛 NullReferenceException
//        → Disconnect (Reason:IncorrectGame / IncorrectVersion)
//    因此手机端整段排除，电脑端保持官方行为。
#if !ANDROID
[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.GetConnectionData))]
public static class HandshakePatch
{
    private const ulong MAGIC = 0x72656163746f72; // "reactor" in ascii, 7 bytes

    public static void Prefix(ref bool useDtlsLayout)
    {
        // Due to reasons currently unknown, the useDtlsLayout parameter sometimes doesn't reflect whether DTLS
        // is actually supposed to be enabled. This causes a bad handshake message and a quick disconnect.
        // The field on AmongUsClient appears to be more reliable, so override this parameter with what it is supposed to be.
        //
        // ⚠️ Brush Up Roles：保留原逻辑，只补空值保护。
        //    原写法 AmongUsClient.Instance.useDtls 在 Instance 为 null 时会抛
        //    NullReferenceException，而 Prefix 跑在游戏原生 GetConnectionData 之前，
        //    异常会被归因到 GetConnectionData 上（与管理员日志的现象一致）。
        try
        {
            var client = AmongUsClient.Instance;

            // 诊断放在 Prefix：Prefix 在原生 GetConnectionData 之前执行，一定能被记录到。
            // （原来放在 Postfix 的记录在安卓上一条都没有 —— 因为原生方法直接抛异常，
            //   Postfix 根本没机会执行。）
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
        // ⚠️ Brush Up Roles：本地模式（LocalGame）与自由游玩（FreePlay）下不发模组握手。
        //
        //    理由：这两种模式根本不经过 matchmaker —— CoConnect 拿到的 matchmakerToken 是空的。
        //    往这种「无 matchmaker 上下文」的连接数据里追加 Reactor 握手段没有意义，
        //    实测也正是在这里抛 NullReferenceException：
        //
        //        Hosting a local game
        //        → GetConnectionData 抛 NullReferenceException
        //        → Disconnect (Reason:IncorrectGame)
        //
        //    只有联机（OnlineGame）才需要让服务器识别模组身份。
        var skip = IsLocalOrFreePlay() || string.IsNullOrEmpty(matchmakerToken);

        // 诊断：下一次日志就能直接看到本地的 token 到底是什么值、有没有跳过。
        Logger.Info(
            $"GetConnectionData: token=\"{(matchmakerToken ?? "<null>")}\" "
            + $"IsLocal={SafeIsLocal()} IsFreePlay={SafeIsFreePlay()} skip={skip}",
            "HandshakePatch");

        if (skip) return;

        if (!Main.HasReactorPlugin)
        {
            var handshake = new MessageWriter(1000);

            // Original data
            handshake.Write(__result);

            // Reactor Header
            var version = (byte)ReactorProtocolVersion.Latest;
            var value = (MAGIC << 8) | version;
            handshake.Write(value);

            // ModdedHandshakeC2S
            handshake.WritePacked(1);
            handshake.Write(Main.PluginGuid);
            handshake.Write(Main.PluginDisplayVersion);
            handshake.Write((ushort)(ModFlags.RequireOnHost | ModFlags.DisableServerAuthority));

            __result = handshake.ToByteArray(true);
            handshake.Recycle();
        }
    }

    /// <summary>
    /// 是否为本地模式 / 自由游玩。
    /// 这两种模式不走 matchmaker，连接数据里不该带模组握手。
    /// 用 try/catch 兜住 —— 静态初始化早期 AmongUsClient.Instance 可能为 null。
    /// </summary>
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