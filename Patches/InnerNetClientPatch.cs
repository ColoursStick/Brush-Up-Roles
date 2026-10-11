using Hazel;
using Il2CppInterop.Runtime.InteropTypes;
using InnerNet;
using System;
using TONE.Modules;

namespace TONE.Patches;

[Obfuscation(Exclude = true)]
public enum GameDataTag : byte
{
    DataFlag = 1,
    RpcFlag = 2,
    SpawnFlag = 4,
    DespawnFlag = 5,
    SceneChangeFlag = 6,
    ReadyFlag = 7,
    ChangeSettingsFlag = 8,
    ConsoleDeclareClientPlatformFlag = 205,
    PS4RoomRequest = 206,
    XboxDeclareXuid = 207,
}

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.HandleGameDataInner))]
internal class GameDataHandlerPatch
{
    public static bool Prefix(InnerNetClient __instance, MessageReader reader, int msgNum)
    {
        if (OperatingSystem.IsAndroid()) return true;

        MessageReader subReader = MessageReader.Get(reader);
        var tag = (GameDataTag)reader.Tag;

        switch (tag)
        {
            case GameDataTag.DataFlag:
                {
                    var netId = reader.ReadPackedUInt32();
                    if (__instance.allObjects.allObjectsFast.TryGetValue(netId, out var obj))
                    {
                        if (obj.AmOwner)
                        {
                            Logger.Warn(string.Format("Received DataFlag for object {0} {1} that we own.", netId.ToString(), obj.name), "GameDataHandlerPatch");
                            EAC.WarnHost();
                            return false;
                        }
                        else
                        {
                            if (AmongUsClient.Instance.AmHost)
                            {
                                if (obj == MeetingHud.Instance)
                                {
                                    Logger.Warn(string.Format("Received DataFlag for MeetingHud {0} that we own.", netId.ToString()), "GameDataHandlerPatch");
                                    EAC.WarnHost();
                                    return false;
                                }

                                if (obj == VoteBanSystem.Instance)
                                {
                                    Logger.Warn(string.Format("Received DataFlag for VoteBanSystem {0} that we own.", netId.ToString()), "GameDataHandlerPatch");
                                    EAC.WarnHost();
                                    return false;
                                }

                                if (obj is NetworkedPlayerInfo)
                                {
                                    Logger.Warn(string.Format("Received DataFlag for NetworkedPlayerInfo {0} that we own.", netId.ToString()), "GameDataHandlerPatch");
                                    EAC.WarnHost();
                                    return false;
                                }
                            }
                        }
                    }

                    break;
                }

            case GameDataTag.RpcFlag:
                break;

            case GameDataTag.SpawnFlag:
                break;

            case GameDataTag.DespawnFlag:
                break;

            case GameDataTag.SceneChangeFlag:
                {
                    // Sender is only allowed to change his own scene.
                    var clientId = reader.ReadPackedInt32();
                    var scene = reader.ReadString();

                    var client = Utils.GetClientById(clientId);

                    if (client == null)
                    {
                        Logger.Warn($"Received SceneChangeFlag for unknown client {clientId}.", "GameDataHandlerPatch");
                        return false;
                    }

                    if (scene == string.Empty || scene == null)
                    {
                        Logger.Warn(string.Format("Client {0} ({1}) tried to send SceneChangeFlag with null scene.", client.PlayerName, client.Id), "GameDataHandlerPatch");
                        EAC.WarnHost();
                        return false;
                    }

                    if (scene.ToLower() == "tutorial")
                    {
                        Logger.Warn(string.Format("Client {0} ({1}) tried to send SceneChangeFlag to Tutorial.", client.PlayerName, client.Id), "GameDataHandlerPatch");
                        EAC.WarnHost(100);

                        if (GameStates.IsOnlineGame && AmongUsClient.Instance.AmHost && GameStates.IsShip && !GameStates.IsLobby)
                        {
                            CriticalErrorManager.SetCriticalError("SceneChange Tutorial Hack", false);
                            CriticalErrorManager.CheckEndGame();
                        }
                        return false;
                    }

                    if (GameStates.IsInGame)
                    {
                        Logger.Warn(string.Format("Client {0} ({1}) tried to send SceneChangeFlag during mid of game.", client.PlayerName, client.Id), "GameDataHandlerPatch");
                        return false;
                    }

                    break;
                }

            case GameDataTag.ReadyFlag:
                {
                    var clientId = reader.ReadPackedInt32();
                    var client = Utils.GetClientById(clientId);

                    if (client == null)
                    {
                        Logger.Warn($"Received ReadyFlag for unknown client {clientId}.", "GameDataHandlerPatch");
                        EAC.WarnHost();
                        return false;
                    }

                    if (AmongUsClient.Instance.AmHost)
                    {
                        if (!StartGameHostPatch.isStartingAsHost)
                        {
                            Logger.Warn($"Received ReadyFlag while game is started from {clientId}.", "GameDataHandlerPatch");
                            EAC.WarnHost();
                            return false;
                        }
                    }

                    break;
                }

            case GameDataTag.ConsoleDeclareClientPlatformFlag:
                break;
        }

        return true;
    }
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.CoStartGameHost))]
internal class StartGameHostPatch
{
    public static bool isStartingAsHost = false;
    public static void Prefix(AmongUsClient __instance)
    {
        if (LobbyBehaviour.Instance != null)
            isStartingAsHost = true;
    }
    public static void Postfix(AmongUsClient __instance)
    {
        Logger.Info("StartGameHostPatch: Postfix called", "StartGameHostPatch");
        if (ShipStatus.Instance != null)
            isStartingAsHost = false;

        GameStates.InGame = true;
    }
}

[HarmonyPatch]
internal class AuthTimeoutPatch
{
    // From Reactor.gg
    // https://github.com/NuclearPowered/Reactor/blob/master/Reactor/Patches/Miscellaneous/CustomServersPatch.cs
    //
    // ⚠️ Brush Up Roles：把这一段从注释状态恢复为启用。
    //
    //    原项目把这两个 Prefix 注释掉了，导致「本地游戏 / 自由游玩 / 原版服务器」也会
    //    走进服务器的认证握手：
    //        AuthManager.CoConnect(...)        ← 连目标服务器要 nonce
    //        AuthManager.CoWaitForNonce(5f)    ← 等服务器回 nonce，默认超时 5 秒
    //    本地游戏根本没有服务器回 nonce，于是必然超时，认证状态不完整，
    //    随后构造连接数据时就崩：
    //
    //        Waiting for time stamp × N          （CoWaitForNonce 在不断重试）
    //        → GetConnectionData → NullReferenceException
    //        → Disconnect (Reason:IncorrectGame)
    //
    //    这正是管理员手机上「本地开房必断连」的原因。
    //
    //    参考实现：Aeterna-End 的 Patches/ClientPatch.cs 里 AuthTimeoutPatch 是**启用**的
    //    （它用 GameStates.CurrentServerTypeInCreateMenu 判断，本工程没有该枚举，
    //      但 GameStates.IsVanillaServer 已包含本地游戏，语义等价）。
    //
    //    注意守卫条件：自定义（HTTP）服务器不在其中，仍会正常走认证握手，不受影响。
    [HarmonyPatch(typeof(AuthManager), nameof(AuthManager.CoConnect))]
    [HarmonyPatch(typeof(AuthManager), nameof(AuthManager.CoWaitForNonce))]
    [HarmonyPrefix]
    public static bool CoWaitforNonce_Prefix()
    {
        return GameStates.IsVanillaServer || GameStates.IsFreePlay || GameStates.IsLocalGame || GameStates.IsNotJoined;
    }

    // If you dont patch this, u still need to wait for 5s
    // I have no idea why this is happening
    [HarmonyPatch]
    public static class EnableUdpPatch
    {
        public static MethodBase TargetMethod()
        {
            return Utils.GetStateMachineMoveNext<AmongUsClient>(nameof(AmongUsClient.CoJoinOnlinePublicGame))!;
        }

        public static void Prefix(Il2CppObjectBase __instance)
        {
            var stateMachine = new StateMachineWrapper<AmongUsClient>(__instance);

            // Skip to state 1 which just calls CoJoinOnlineGameDirect
            if (stateMachine.State == 0 && !ServerManager.Instance.IsHttp)
            {
                stateMachine.State = 1;
                var lambdaType = stateMachine.GetParameter<Il2CppObjectBase>("__8__1").GetType();
                var newDisplayClass = Activator.CreateInstance(lambdaType);
                if (newDisplayClass == null)
                {
                    throw new InvalidOperationException($"Could not create display class of type '{lambdaType}'.");
                }

                var displayClass = new CompilerGeneratedObjectWrapper(newDisplayClass);
                displayClass.SetField("matchmakerToken", string.Empty);

                stateMachine.SetParameter("__8__1", newDisplayClass);
            }
        }
    }
}
[HarmonyPatch(typeof(NetworkedPlayerInfo), nameof(NetworkedPlayerInfo.UpdateName))]
public class NetworkedPlayerInfoPatch
{
    // Prevent mark dirty here
    public static bool Prefix(NetworkedPlayerInfo __instance, string playerName, ClientData client)
    {
        __instance.PlayerName = playerName;
        client.UpdatePlayerName(playerName);
        return false;
    }
}