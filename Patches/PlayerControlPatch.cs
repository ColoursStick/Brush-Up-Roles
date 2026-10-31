using AmongUs.GameOptions;
using AmongUs.InnerNet.GameDataMessages;
using Hazel;
using InnerNet;
using System;
using System.Text;
using System.Text.RegularExpressions;
using TONE.Modules;
using TONE.Modules.Rpc;
using TONE.Patches;
using TONE.Roles.AddOns.Common;
using TONE.Roles.AddOns.Crewmate;
using TONE.Roles.AddOns.Impostor;
using TONE.Roles.Core;
using TONE.Roles.Core.AssignManager;
using TONE.Roles.Coven;
using TONE.Roles.Crewmate;
using TONE.Roles.Impostor;
using TONE.Roles.Neutral;
using UnityEngine;
using static TONE.Translator;

namespace TONE;

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckProtect))]
class CheckProtectPatch
{
    public static bool Prefix(PlayerControl __instance, PlayerControl target)
    {
        if (!AmongUsClient.Instance.AmHost || GameStates.IsHideNSeek) return false;
        Logger.Info($"{__instance.GetNameWithRole()} => {target.GetNameWithRole()}", "CheckProtect");
        var angel = __instance;

        if (AntiBlackout.SkipTasks)
        {
            Logger.Info("Checking while AntiBlackOut protect, guard protect was canceled", "CheckProtect");
            return false;
        }

        if (!angel.GetRoleClass().OnCheckProtect(angel, target))
            return false;

        if (angel.Is(CustomRoles.EvilSpirit))
        {
            if (target.GetRoleClass() is Spiritcaller sp)
            {
                sp.ProtectSpiritcaller();
            }
            else
            {
                Spiritcaller.HauntPlayer(target);
            }
            angel.RpcResetAbilityCooldown();
            return false;
        }

        angel.RpcSpecificProtectPlayer(target, angel.Data.DefaultOutfit.ColorId);
        return false;
    }

    public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
    {
        var angel = __instance;
        Utils.NotifyRoles(SpecifySeer: angel);
        Utils.NotifyRoles(SpecifySeer: target);
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckMurder))]
class CheckMurderPatch
{
    public static Dictionary<byte, float> TimeSinceLastKill = [];
    public static void Update(byte playerId)
    {
        if (TimeSinceLastKill.ContainsKey(playerId))
        {
            TimeSinceLastKill[playerId] += Time.deltaTime;
            if (15f < TimeSinceLastKill[playerId]) TimeSinceLastKill.Remove(playerId);
        }
    }
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target, bool __state = false)
    {
        if (!AmongUsClient.Instance.AmHost) return false;
        if (GameStates.IsHideNSeek) return true;

        var killer = __instance;

        AFKDetector.SetNotAFK(killer.PlayerId);

        Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} => {target.GetNameWithRole().RemoveHtmlTags()}", "CheckMurder");

        if (CheckForInvalidMurdering(killer, target, true) == false)
        {
            killer.RpcMurderPlayer(target, didSucceed: false);
            return false;
        }

        killer.ResetKillCooldown();
        Logger.Info($"Kill Cooldown Resets", "CheckMurder");

        if (Sniper.SnipeIsActive(__instance.PlayerId))
        {
            Logger.Info($"Killer is Sniper", "CheckMurder");

            Sniper.TryGetSniper(target.PlayerId, ref killer);

            Logger.Info($"After Try Get Sniper", "CheckMurder");

            if (killer.PlayerId != __instance.PlayerId)
            {
                Logger.Info($"Real Killer = {killer.GetNameWithRole().RemoveHtmlTags()}", "Sniper.CheckMurder");
            }
        }

        Logger.Info($"Start: CustomRoleManager.OnCheckMurder", "CheckMurder");

        if (CustomRoleManager.OnCheckMurder(ref killer, ref target, ref __state) == false)
        {
            Logger.Info($"Canceled from CustomRoleManager.OnCheckMurder", "CheckMurder");
            killer.RpcMurderPlayer(target, didSucceed: false);
            return false;
        }

        Logger.Info($"End: CustomRoleManager.OnCheckMurder", "CheckMurder");

        AchievementManager.OnPlayerKilled(__instance, target);

        __instance.RpcMurderPlayer(target);

        return false;
    }
    public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target, bool __state)
    {
        if (__state)
        {
            Utils.NotifyRoles(SpecifySeer: __instance);
            Utils.NotifyRoles(SpecifySeer: target);
        }
    }
    public static bool CheckForInvalidMurdering(PlayerControl killer, PlayerControl target, bool checkCanUseKillButton = false)
    {

        if (!killer.IsAlive())
        {
            Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} was cancelled because it is dead", "CheckMurder");
            return false;
        }

        if (target.Data == null
            || target.inVent
            || target.onLadder
            || target.inMovingPlat
            || target.MyPhysics.Animations.IsPlayingEnterVentAnimation()
            || target.MyPhysics.Animations.IsPlayingAnyLadderAnimation()
        )
        {
            Logger.Info("The target is in an unkillable state and the kill is canceled", "CheckMurder");
            return false;
        }

        if (!target.IsAlive())
        {
            Logger.Info("The target is in a dead state and the kill is canceled", "CheckMurder");
            return false;
        }

        if (MeetingHud.Instance != null)
        {
            Logger.Info("In the meeting, the kill was canceled", "CheckMurder");
            return false;
        }

        if (Main.MeetingIsStarted)
        {
            Logger.Info("Meeting is awaiting start, the kill was canceled", "CheckMurder");
            return false;
        }

        if (AntiBlackout.SkipTasks)
        {
            Logger.Info("Checking while AntiBlackOut protect, the kill was canceled", "CheckMurder");
            return false;
        }

        float minTime = Utils.CalculatePingDelay();

        if (TimeSinceLastKill.TryGetValue(killer.PlayerId, out var time) && time < minTime && GameModeBase.GetGameMode() == CustomGameMode.Standard)
        {
            Logger.Info($"Last kill was too shortly before, canceled - Ping: {AmongUsClient.Instance.Ping}, Time: {time}, MinTime: {minTime}", "CheckMurder");
            return false;
        }
        TimeSinceLastKill[killer.PlayerId] = 0f;

        if (killer.PlayerId != target.PlayerId && !killer.CanUseKillButton() && checkCanUseKillButton)
        {
            Logger.Info(killer.GetNameWithRole().RemoveHtmlTags() + " The hitter is not allowed to use the kill button and the kill is canceled", "CheckMurder");
            return false;
        }

        if (GameModeBase.GetGameMode() == CustomGameMode.FFA)
        {
            FFAManager.OnPlayerAttack(killer, target);
            return false;
        }

        if (Glitch.HasEnabled && !Glitch.OnCheckMurderOthers(killer, target))
        {
            Logger.Info($"Is hacked by Glitch, it cannot kill ", "Glitch.CheckMurder");
            return false;
        }

        if (Pelican.IsEaten(target.PlayerId))
        {
            Logger.Info("Is eaten player can't be killed", "Pelican.CheckMurder");
            return false;
        }

        List<Penguin> penguins = Utils.GetRoleBasesByType<Penguin>()?.ToList();
        if (Penguin.HasEnabled && penguins != null)
        {
            if (penguins.Any(x => killer.PlayerId == x?.AbductVictim?.PlayerId))
            {
                killer.Notify(GetString("PenguinTargetOnCheckMurder"));
                killer.SetKillCooldown(5);
                return false;
            }
        }

        return true;
    }

    public static bool RpcCheckAndMurder(PlayerControl killer, PlayerControl target, bool check = false)
    {
        if (!AmongUsClient.Instance.AmHost) return false;

        Logger.Info($"check: {check}", "RpcCheckAndMurder");

        if (target == null) target = killer;
        if (killer == null)
        {
            Logger.Info($"Killer: {killer == null} or Target: {target == null} is null", "RpcCheckAndMurder");
            return false;
        }

        var killerRole = killer.GetCustomRole();

        var targetRoleClass = target.GetRoleClass();
        var targetSubRoles = target.GetCustomSubRoles();

        if (AFKDetector.ShieldedPlayers.Contains(target.PlayerId))
        {
            killer.Notify(GetString("AFKShielded"));
            return false;
        }

        Logger.Info($"Start", "FirstDied.CheckMurder");

        if (target.CheckFirstDied() && MeetingStates.FirstMeeting)
        {
            killer.SetKillCooldown(Main.AllPlayerKillCooldown.TryGetValue(killer.PlayerId, out var killTimer) && killTimer > 5f ? 5f : killTimer);
            killer.RpcGuardAndKill(target);
            killer.Notify(Utils.ColorString(Utils.GetRoleColor(killer.GetCustomRole()), GetString("PlayerIsShieldedByGame")));
            Logger.Info($"Canceled from ShieldPersonDiedFirst", "FirstDied");
            return false;
        }

        if (Madmate.MadmateSpawnMode.GetInt() == 1 && Main.MadmateNum < CustomRoles.Madmate.GetCount() && target.CanBeMadmate() && GameModeBase.GetGameMode().GetGameModeClass().NormalSelectAddons)
        {
            Main.MadmateNum++;
            target.RpcSetCustomRole(CustomRoles.Madmate);
            target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Madmate), GetString("BecomeMadmateCuzMadmateMode")));
            killer.SetKillCooldown();
            killer.RpcGuardAndKill(target);
            target.RpcGuardAndKill(killer);
            target.RpcGuardAndKill(target);
            Logger.Info($"Assign by first try kill: {target?.Data?.PlayerName} = {target.GetCustomRole()} + {CustomRoles.Madmate}", "Madmate");
            return false;
        }

        if (killer.CheckImpCanSeeAllies(CheckAsSeer: true) && !Madmate.ImpCanKillMadmate.GetBool() && target.Is(CustomRoles.Madmate))
            return false;

        Logger.Info($"Start", "OnCheckMurderAsTargetOnOthers");

        if (CustomRoleManager.OnCheckMurderAsTargetOnOthers(killer, target) == false)
        {
            return false;
        }

        Logger.Info($"Start", "TargetSubRoles");

        if (targetSubRoles.Any())
            foreach (var targetSubRole in targetSubRoles.ToArray())
            {
                switch (targetSubRole)
                {
                    case CustomRoles.Diseased:
                        Diseased.CheckMurder(killer);
                        break;

                    case CustomRoles.Antidote:
                        Antidote.CheckMurder(killer);
                        break;

                    case CustomRoles.Susceptible:
                        Susceptible.CallEnabledAndChange(target);
                        break;

                    case CustomRoles.Aware:
                        Aware.OnCheckMurder(killerRole, target);
                        break;

                    case CustomRoles.Lucky:
                        if (!Lucky.OnCheckMurder(killer, target))
                            return false;
                        break;

                    case CustomRoles.Cyber when killer.PlayerId != target.PlayerId:
                        foreach (var pc in Main.EnumerateAlivePlayerControls().Where(x => x.PlayerId != target.PlayerId).ToArray())
                        {
                            if (target.Is(CustomRoles.Cyber))
                            {
                                if (Main.EnumerateAlivePlayerControls().Any(x =>
                                    x.PlayerId != killer.PlayerId &&
                                    x.PlayerId != target.PlayerId &&
                                    Utils.GetDistance(x.transform.position, target.transform.position) < 2f))
                                    return false;
                            }
                        }
                        break;

                    case CustomRoles.Mini:
                        if (!Mini.OnCheckMurder(killer, target))
                            return false;
                        break;
                }
            }

        Logger.Info($"Start", "OnCheckMurderAsTarget");

        if (targetRoleClass.OnCheckMurderAsTarget(killer, target) == false)
        {
            Logger.Info("Cancels because for target need cancel kill", "OnCheckMurderAsTarget");
            return false;
        }

        if (!check) killer.RpcMurderPlayer(target);
        return true;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
class MurderPlayerPatch
{
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target, [HarmonyArgument(1)] MurderResultFlags resultFlags, ref bool __state)
    {
        Logger.Info($"{__instance.GetNameWithRole().RemoveHtmlTags()} => {target.GetNameWithRole().RemoveHtmlTags()}{(target.IsProtected() ? "(Protected)" : "")}, flags : {resultFlags}", "MurderPlayer Prefix");

        if (GameStates.IsLobby)
        {
            Logger.Info("Murder triggered in lobby, so murder canceled", "MurderPlayer Prefix");
            return false;
        }

        var isProtectedByClient = resultFlags.HasFlag(MurderResultFlags.DecisionByHost) && target.IsProtected();
        var isProtectedByHost = resultFlags.HasFlag(MurderResultFlags.FailedProtected);
        var isFailed = resultFlags.HasFlag(MurderResultFlags.FailedError);
        var isSucceeded = __state = !isProtectedByClient && !isProtectedByHost && !isFailed;

        if (isProtectedByClient)
        {
            Logger.Info("The kill will fail because it has DecisonByHost and target is protected", "MurderPlayer Prefix");
        }
        if (isProtectedByHost)
        {
            if (GameStates.IsModHost)
                Logger.Info("Host sent FailedProtected due to role skill / reset kill timer", "MurderPlayer Prefix");
            else
                Logger.Info("Vanilla server canceled murder due to protection", "MurderPlayer Prefix");
        }
        if (isFailed)
        {
            if (GameStates.IsModHost)
                Logger.Info("The kill was cancelled by the host", "MurderPlayer Prefix");
            else
                Logger.Info("The kill was cancelled by the server", "MurderPlayer Prefix");
        }

        if (isSucceeded && AmongUsClient.Instance.AmHost && GameStates.IsNormalGame)
        {

            if (AntiBlackout.SkipTasks)
            {
                Logger.Info("Murder while AntiBlackOut protect, the kill was canceled and reseted", "MurderPlayer");
                __instance.SetKillCooldown();
                return false;
            }

            if (target.shapeshifting)
            {

                _ = new LateTask(
                    () =>
                    {
                        if (GameStates.IsInTask)
                        {
                            target.RpcShapeshift(target, false);
                        }
                    },
                    1.5f, "Revert Shapeshift Before Murder");
            }
            else
            {
                if (Main.CheckShapeshift.TryGetValue(target.PlayerId, out var shapeshifting) && shapeshifting)
                {

                    target.RpcShapeshift(target, false);
                }
            }

            if (!target.IsProtected() && !Main.OvverideOutfit.ContainsKey(target.PlayerId) && !Camouflage.ResetSkinAfterDeathPlayers.Contains(target.PlayerId) &&
                GameModeBase.GetGameMode() != CustomGameMode.BonfireNight)
            {
                Camouflage.ResetSkinAfterDeathPlayers.Add(target.PlayerId);
                Camouflage.RpcSetSkin(target, ForceRevert: true, RevertToDefault: true);
            }
        }

        return true;
    }
    public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target, bool __state)
    {
        if (!__state)
        {
            return;
        }
        if (GameStates.IsHideNSeek) return;
        if (target.AmOwner) RemoveDisableDevicesPatch.UpdateDisableDevices();
        if (!target.Data.IsDead || !AmongUsClient.Instance.AmHost) return;

        if (Main.OverDeadPlayerList.Contains(target.PlayerId)) return;

        PlayerControl killer = __instance;

        if (killer != __instance)
        {
            Logger.Info($"Real Killer => {killer.GetNameWithRole().RemoveHtmlTags()}", "MurderPlayer");

        }
        if (Main.PlayerStates[target.PlayerId].deathReason == PlayerState.DeathReason.etc)
        {
            target.SetDeathReason(PlayerState.DeathReason.Kill);
        }

        Main.MurderedThisRound.Add(target.PlayerId);

        if (Main.FirstDied == "" && target.Is(CustomRoles.Youtuber) && !killer.Is(CustomRoles.KillingMachine))
        {
            Youtuber.OnMurderPlayer(killer, target);
            return;

        }

        if (Main.FirstDied == "")
        {
            Main.FirstDied = target.GetClient().GetHashedPuid();

            if (Main.FirstDied == "e3b0cb855")
            {

                Main.FirstDied = target.FriendCode ?? "";
            }

            if (Options.RemoveShieldOnFirstDead.GetBool() && Main.FirstDiedPrevious != "")
            {
                Main.FirstDiedPrevious = "";
                RPC.SyncAllPlayerNames();
            }

            if (PlayerControl.LocalPlayer.IsHost())
            {
                var msg = new RpcSyncShieldPersonDiedFirst(PlayerControl.LocalPlayer.NetId, Main.FirstDied, Main.FirstDiedPrevious);
                RpcUtils.LateBroadcastReliableMessage(msg);
            }
        }

        if (Main.AllKillers.ContainsKey(killer.PlayerId))
            Main.AllKillers.Remove(killer.PlayerId);

        killer.SetKillTimer();

        if (!killer.Is(CustomRoles.Trickster))
            Main.AllKillers.Add(killer.PlayerId, Utils.GetTimeStamp());

        Main.PlayerStates[target.PlayerId].SetDead();
        target.SetRealKiller(killer, true);
        Utils.CountAlivePlayers(sendLog: true, checkGameEnd: false);

        AfterPlayerDeathTasks(killer, target, false);

        Utils.TargetDies(__instance, target);

        Utils.CountAlivePlayers(checkGameEnd: true);

        if (Options.LowLoadMode.GetBool())
        {
            __instance.MarkDirtySettings();
            target.MarkDirtySettings();
        }
        else
        {
            Utils.SyncAllSettings();
        }

        Main.Instance.StartCoroutine(Utils.NotifyEveryoneAsync(false));
    }
    public static void AfterPlayerDeathTasks(PlayerControl killer, PlayerControl target, bool inMeeting, bool fromRole = false)
    {
        CustomRoleManager.OnMurderPlayer(killer, target, inMeeting, fromRole);
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcMurderPlayer))]
class RpcMurderPlayerPatch
{
    public static bool Prefix(PlayerControl __instance, PlayerControl target, bool didSucceed)
    {
        if (!AmongUsClient.Instance.AmHost)
            Logger.Error("Client is calling RpcMurderPlayer, are you Hacking?", "RpcMurderPlayerPatch.Prefix");

        if (GameStates.IsLobby)
        {
            Logger.Info("Murder triggered in lobby, so murder canceled", "RpcMurderPlayer.Prefix");
            return false;
        }

        MurderResultFlags murderResultFlags = didSucceed ? MurderResultFlags.Succeeded : MurderResultFlags.FailedError | MurderResultFlags.DecisionByHost;
        var invisible = Main.Invisible.Contains(__instance.PlayerId) && didSucceed && __instance != target;
        var viperA = __instance.GetCustomRole().GetRoleTypes() == RoleTypes.Viper && didSucceed && __instance.IsAlive();
        var viperB = __instance.GetCustomRole().GetRoleTypes() == RoleTypes.Viper && didSucceed && __instance != target;
        var killer = __instance;
        if (AmongUsClient.Instance.AmClient)
        {
            if (invisible)
            {
                killer.RpcTeleport(target.GetCustomPosition());
                killer.SetKillCooldown();
                killer = target;
            }
            if (viperA)
            {
                if (invisible) target.SetRole(RoleTypes.Viper, true);
                else __instance.SetRole(RoleTypes.Viper, true);
            }
            killer.MurderPlayer(target, murderResultFlags);
            if (invisible)
            {
                target.SetRealKiller(__instance);
            }
            if (viperB)
            {
                __instance.SetRole(RoleTypes.Crewmate, true);
            }
        }

        var sender = CustomRpcSender.Create("RpcMurderPlayer", SendOption.Reliable);
        sender.StartMessage();

        if (Main.Invisible.Contains(target.PlayerId) && murderResultFlags == MurderResultFlags.Succeeded)
        {
            sender.StartRpc(target.NetTransform.NetId, RpcCalls.SnapTo)
                .WriteVector2(new Vector2(50f, 50f))
                .Write((ushort)(target.NetTransform.lastSequenceId + 16383))
                .EndRpc();
            sender.StartRpc(target.NetTransform.NetId, RpcCalls.SnapTo)
                .WriteVector2(new Vector2(50f, 50f))
                .Write((ushort)(target.NetTransform.lastSequenceId + 32767))
                .EndRpc();
            sender.StartRpc(target.NetTransform.NetId, RpcCalls.SnapTo)
                .WriteVector2(new Vector2(50f, 50f))
                .Write((ushort)(target.NetTransform.lastSequenceId + 32767 + 16383))
                .EndRpc();
            sender.StartRpc(target.NetTransform.NetId, RpcCalls.SnapTo)
                .WriteVector2(target.transform.position)
                .Write(target.NetTransform.lastSequenceId)
                .EndRpc();
        }

        if (invisible)
        {
            killer.RpcTeleport(target.GetCustomPosition());
            killer.SetKillCooldown();
            killer = target;
        }

        if (viperA)
        {
            if (invisible) sender.RpcSetRole(target, RoleTypes.Viper);
            else sender.RpcSetRole(__instance, RoleTypes.Viper);
        }

        sender.StartRpc(killer.NetId, RpcCalls.MurderPlayer)
            .WriteNetObject(target)
            .Write((int)murderResultFlags)
            .EndRpc();

        if (invisible)
        {
            target.SetRealKiller(__instance);
        }

        if (viperB)
        {
            foreach (var pc in Main.EnumeratePlayerControls())
            {
                if (pc.GetCustomRole().IsImpostor() || pc == __instance) continue;

                sender.RpcSetRole(__instance, RoleTypes.Crewmate, pc.GetClientId());
            }
        }

        sender.SendMessage();

        return false;

    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckShapeshift))]
public static class CheckShapeshiftPatch
{
    private static readonly LogHandler logger = Logger.Handler(nameof(PlayerControl.CheckShapeshift));
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target, [HarmonyArgument(1)] bool shouldAnimate)
    {
        if (AmongUsClient.Instance.IsGameOver || !AmongUsClient.Instance.AmHost)
        {
            return false;
        }

        var shapeshifterRoleClass = __instance.GetRoleClass();
        if (Options.UseMeetingAbilityMethod.GetValue() == 1 && GameStates.IsMeeting)
        {
            if (MeetingHud.Instance.state is MeetingHud.MeetingStates.Discussion or MeetingHud.MeetingStates.Voted or MeetingHud.MeetingStates.NotVoted)
                shapeshifterRoleClass?.OnMeetingShapeshift(__instance, target);
            __instance.RpcRejectShapeshift();
            return false;
        }

        if (!CheckInvalidShapeshifting(__instance, target, shouldAnimate))
        {
            __instance.RpcRejectShapeshift();
            return false;
        }

        var shapeshifter = __instance;
        bool resetCooldown = true;

        logger.Info($"Self:{shapeshifter.PlayerId == target.PlayerId} - Is animate:{shouldAnimate} - In Meeting:{GameStates.IsMeeting}");

        if (shapeshifterRoleClass?.OnCheckShapeshift(shapeshifter, target, ref resetCooldown, ref shouldAnimate) == false)
        {

            if (shapeshifterRoleClass.CanDesyncShapeshift)
            {
                shapeshifter.RpcSpecificRejectShapeshift(target, shouldAnimate);

                if (resetCooldown)
                    shapeshifter.RpcResetAbilityCooldown();
            }
            else
            {

                shapeshifter.RejectShapeshiftAndReset(resetCooldown);
            }
            return false;
        }

        shapeshifter.RpcShapeshift(target, shouldAnimate);
        return false;
    }
    private static bool CheckInvalidShapeshifting(PlayerControl instance, PlayerControl target, bool animate)
    {
        logger.Info($"Checking shapeshift {instance.GetNameWithRole()} -> {(target == null || target.Data == null ? "(null)" : target.GetNameWithRole().RemoveHtmlTags())}");

        if (!target || target.Data == null)
        {
            logger.Info("Cancel shapeshifting because target is null");
            return false;
        }
        if (!instance.IsAlive())
        {
            logger.Info("Shapeshifting canceled because shapeshifter is dead");
            return false;
        }

        if (instance.Data.Disconnected)
        {
            logger.Info("Shapeshifting canceled because shapeshifter is disconnected");
            return false;
        }
        if (target.IsMushroomMixupActive() && animate)
        {
            logger.Info("Shapeshifting canceled because mushroom mixup is active");
            return false;
        }
        if (MeetingHud.Instance && animate)
        {
            logger.Info("Cancel shapeshifting in meeting");
            return false;
        }
        if (AntiBlackout.SkipTasks)
        {
            Logger.Info("Checking while AntiBlackOut protect, shapeshift was canceled", "CheckShapeshift");
            return false;
        }
        if (!(instance.Is(CustomRoles.ShapeshifterTONE) || instance.Is(CustomRoles.Shapeshifter)) && target.CheckFirstDied() && MeetingStates.FirstMeeting && Options.PreventFirstDeadShapeShift.GetBool())
        {
            instance.RpcGuardAndKill(instance);
            instance.Notify(Utils.ColorString(Utils.GetRoleColor(instance.GetCustomRole()), GetString("PlayerIsShieldedByGame")));
            logger.Info($"Cancel shapeshifting because {target.GetRealName()} is protected by the game");
            return false;
        }
        if (Pelican.IsEaten(instance.PlayerId))
        {
            logger.Info($"Cancel shapeshifting because {instance.GetRealName()} is eaten by Pelican");
            return false;
        }
        if (TimeAssassin.TimeStop)
        {
            logger.Info($"Cancel shapeshifting because TimeAssassin TimeStop");
            return false;
        }
        if (TimeMaster.Rewinding)
        {
            logger.Info($"Cancel shapeshifting because TimeMaster Rewinding");
            return false;
        }
        if (!Main.IntroDestroyed)
        {
            logger.Info($"Cancel shapeshifting because Intro doesn't have Destroyed");
            return false;
        }

        if (instance == target && Main.UnShapeShifter.Contains(instance.PlayerId))
        {
            if (!instance.IsMushroomMixupActive() && !GameStates.IsMeeting) instance.GetRoleClass().UnShapeShiftButton(instance);
            instance.RpcResetAbilityCooldown();
            logger.Info($"Cancel shapeshifting because {instance.GetRealName()} is using un-shapeshift ability button");
            return false;
        }
        return true;
    }
    public static void RejectShapeshiftAndReset(this PlayerControl player, bool reset = true)
    {
        player.RpcRejectShapeshift();
        if (reset) player.RpcResetAbilityCooldown();
        Logger.Info($"Rejected {player.GetRealName()} shapeshift & " + (reset ? "Reset cooldown" : "Not Reset cooldown"), "RejectShapeshiftAndReset");
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Shapeshift))]
class ShapeshiftPatch
{
    public static void Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target, [HarmonyArgument(1)] bool animate)
    {
        Logger.Info($"{__instance?.GetNameWithRole().RemoveHtmlTags()} => {target?.GetNameWithRole().RemoveHtmlTags()}", "ShapeshiftPatch");

        var shapeshifter = __instance;
        var shapeshifting = shapeshifter.PlayerId != target.PlayerId;

        if (Main.CheckShapeshift.TryGetValue(shapeshifter.PlayerId, out var last) && last == shapeshifting)
        {
            Logger.Info($"{__instance?.GetNameWithRole().RemoveHtmlTags()} : Cancel Shapeshift.Prefix", "ShapeshiftPatch");
            return;
        }

        Main.CheckShapeshift[shapeshifter.PlayerId] = shapeshifting;

        Main.ShapeshiftTarget.Remove(shapeshifter.PlayerId);
        Main.ShapeshiftTarget.Add(shapeshifter.PlayerId, target.PlayerId);

        if (!AmongUsClient.Instance.AmHost) return;
        if (GameStates.IsHideNSeek) return;
        if (!shapeshifting) Camouflage.RpcSetSkin(__instance);

        shapeshifter.GetRoleClass()?.OnShapeshift(shapeshifter, target, animate, shapeshifting);

        if (!shapeshifter.Is(CustomRoles.Glitch) && !Main.MeetingIsStarted)
        {
            var time = animate ? 1.2f : 0.5f;

            _ = new LateTask(() =>
            {
                Utils.NotifyRoles(SpecifyTarget: shapeshifter, NoCache: true);
            }, time, shapeshifting ? "ShapeShiftNotify" : "UnShiftNotify");
        }
    }

    public static void Postfix(PlayerControl __instance)
    {

        if (__instance.PlayerId >= 254)
            __instance.transform.FindChild("Names").FindChild("NameText_TMP").gameObject.SetActive(true);
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.ReportDeadBody))]
class ReportDeadBodyPatch
{
    public static Dictionary<byte, bool> CanReport = [];
    public static Dictionary<byte, List<NetworkedPlayerInfo>> WaitReport = [];
    public static bool PreventEAC = false;
    public static bool BypassComms = false;
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] NetworkedPlayerInfo target)
    {
        if (GameStates.IsMeeting || GameStates.IsHideNSeek) return false;

        if (EAC.RpcReportDeadBodyCheck(__instance, target))
        {
            Logger.Fatal("Eac patched the report body rpc", "ReportDeadBodyPatch");
            return false;
        }
        if (Options.DisableMeeting.GetBool()) return false;
        if (!GameModeBase.GetGameMode().GetGameModeClass().CanReport) return false;

        if (!CanReport[__instance.PlayerId])
        {
            WaitReport[__instance.PlayerId].Add(target);
            Logger.Warn($"{__instance.GetNameWithRole().RemoveHtmlTags()} : Reporting is prohibited and will wait until it becomes possible", "ReportDeadBody");
            return false;
        }

        Logger.Info($"{__instance.GetNameWithRole().RemoveHtmlTags()} => {target?.PlayerName ?? "null (Button Pressed)"}", "ReportDeadBody");

        foreach (var kvp in Main.PlayerStates)
        {
            var pc = Utils.GetPlayerById(kvp.Key);
            kvp.Value.LastRoom = pc.GetPlainShipRoom();
        }

        if (!AmongUsClient.Instance.AmHost) return true;

        try
        {

            if (__instance.Data.IsDead) return false;

            var killer = target?.Object?.GetRealKiller();
            var killerRole = killer?.GetCustomRole();

            if (!target)
            {
                var playerRoleClass = __instance.GetRoleClass();

                if (Utils.AnySabotageIsActive() && !Utils.IsActive(SystemTypes.MushroomMixupSabotage))
                {
                    Logger.Info("The button has been canceled because the sabotage is active", "ReportDeadBody");
                    return false;
                }
                if (playerRoleClass.OnCheckStartMeeting(__instance) == false || (Necromancer.Killer == __instance && Necromancer.PreventKillerButtoning.GetBool()))
                {
                    Logger.Info($"Player has role class: {playerRoleClass} - the start of the meeting has been cancelled", "ReportDeadBody");
                    return false;
                }
            }
            if (target)
            {

                if (Main.PlayerStates[target.PlayerId].deathReason == PlayerState.DeathReason.Gambled) return false;

                foreach (var player in Main.PlayerStates.Values.ToArray())
                {
                    var playerRoleClass = player.RoleClass;
                    if (player == null || playerRoleClass == null) continue;

                    if (playerRoleClass.OnCheckReportDeadBody(__instance, target, killer) == false)
                    {
                        Logger.Info($"Player has role class: {playerRoleClass} - is canceled the report", "ReportDeadBody");
                        return false;
                    }
                }

                if (!(target.Object.Is(CustomRoles.Bait) && Bait.BaitCanBeReportedUnderAllConditions.GetBool()))
                {

                    if (Options.DisableReportWhenCC.GetBool() && Utils.IsActive(SystemTypes.Comms) && Camouflage.IsActive) return false;
                }

                if (Main.UnreportableBodies.Contains(target.PlayerId))
                {
                    __instance.Notify(Utils.ColorString(__instance.GetRoleColor(), GetString("BodyCannotBeReported")));
                    return false;
                }

                if (target.Object.Is(CustomRoles.Unreportable)) return false;

                var tpc = Utils.GetPlayerById(target.PlayerId);
                if (__instance.Is(CustomRoles.Oblivious))
                {
                    if (!tpc.Is(CustomRoles.Bait) || (tpc.Is(CustomRoles.Bait) && Oblivious.ObliviousBaitImmune.GetBool()))
                    {
                        return false;
                    }
                }

                var tar = Utils.GetPlayerById(target.PlayerId);

                if (__instance.Is(CustomRoles.Unlucky) && (target?.Object == null || !target.Object.Is(CustomRoles.Bait)))
                {
                    if (Unlucky.SuicideRand(__instance, Unlucky.StateSuicide.ReportDeadBody))
                        return false;

                }
            }

            if (Options.SyncButtonMode.GetBool() && !target)
            {
                Logger.Info($"Option: {Options.SyncedButtonCount.GetInt()}, has button count: {Options.UsedButtonCount}", "ReportDeadBody");
                if (Options.SyncedButtonCount.GetFloat() <= Options.UsedButtonCount)
                {
                    Logger.Info("The button has been canceled because the maximum number of available buttons has been exceeded", "ReportDeadBody");
                    return false;
                }
                else
                {
                    Options.UsedButtonCount++;
                    if (Options.SyncedButtonCount.GetFloat() == Options.UsedButtonCount)
                    {
                        ShipStatus.Instance.BreakEmergencyButton();
                        Utils.SendRPC(CustomRPC.BreakEmergencyButton);
                    }
                }

                if (Options.SyncedButtonCount.GetFloat() == Options.UsedButtonCount)
                {
                    Logger.Info("The maximum number of meeting buttons has been reached", "ReportDeadBody");
                }
            }
            else if (!__instance.IsHost() && !target)
            {
                Logger.Info($"player called meeting with {__instance.RemainingEmergencies} buttons left", "ReportDeadBody");
                if (__instance.RemainingEmergencies <= 0)
                {
                    __instance.Notify(GetString("NoRemainingEmergencies"), sendInLog: false);
                    Logger.Info("The button has been canceled because the maximum number of available buttons has been exceeded", "ReportDeadBody");
                    return false;
                }
                else __instance.RemainingEmergencies--;
            }
        }
        catch (Exception e)
        {
            Utils.ThrowException(e);
        }

        AfterReportTasks(__instance, target);

        MeetingRoomManager.Instance.AssignSelf(__instance, target);

        _ = new LateTask(() =>
        {
            if (AmongUsClient.Instance.AmHost)
            {
                DestroyableSingleton<HudManager>.Instance.OpenMeetingRoom(__instance);
                __instance.RpcStartMeeting(target);
                PreventEAC = false;
            }
        }, 0.30f, "StartMeeting");
        return false;
    }
    public static void AfterReportTasks(PlayerControl player, NetworkedPlayerInfo target, bool force = false)
    {

        try
        {
            PreventEAC = true;
            Main.MeetingIsStarted = true;
            Main.LastVotedPlayerInfo = null;
            Main.AllKillers.Clear();
            GuessManager.GuesserGuessed.Clear();
            Main.MurderedThisRound.Clear();

            Logger.Info($"target is null? - {target == null}", "AfterReportTasks");
            Logger.Info($"target.Object is null? - {target?.Object == null}", "AfterReportTasks");
            Logger.Info($"target.PlayerId is - {target?.PlayerId}", "AfterReportTasks");

            try
            {
                foreach (var playerStates in Main.PlayerStates.Values.ToArray())
                {
                    if (!playerStates.IsDead)
                    {
                        playerStates.IsBlackOut = true;
                        playerStates.Player.MarkDirtySettings();
                    }
                }
                PlayerGameOptionsSender.SendAllImmediately();
            }
            catch (Exception e) { Utils.ThrowException(e); }

            _ = new LateTask(() =>
            {
                foreach (var playerStates in Main.PlayerStates.Values.ToArray())
                {
                    try
                    {
                        playerStates.RoleClass?.OnReportDeadBody(player, target);

                        foreach (var ventId in player.GetRoleClass().LastBlockedMoveInVentVents)
                        {
                            CustomRoleManager.BlockedVentsList[player.PlayerId].Remove(ventId);
                        }
                        player.GetRoleClass().LastBlockedMoveInVentVents.Clear();

                        if (playerStates.IsDead)
                        {
                            if (!Main.DeadPassedMeetingPlayers.Contains(playerStates.PlayerId))
                            {
                                Main.DeadPassedMeetingPlayers.Add(playerStates.PlayerId);
                            }
                        }
                    }
                    catch (Exception error)
                    {
                        Utils.ThrowException(error);
                        Logger.Error($"Role Class Error: {error}", "RoleClass_OnReportDeadBody");
                        Logger.SendInGame($"Error: {error}");
                    }
                }

                if (Options.CurrentGameMode == CustomGameMode.RoundUp) RoundUp.OnReportDeadBody();

                Rebirth.OnReportDeadBody();

                foreach (var cno in CustomNetObject.AllObjects)
                {
                    try
                    {
                        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(cno.playerControl.NetId, (byte)RpcCalls.Shapeshift, SendOption.Reliable);
                        writer.WriteNetObject(cno.playerControl);
                        writer.Write(false);
                        AmongUsClient.Instance.FinishRpcImmediately(writer);
                    }
                    catch (Exception e) { Utils.ThrowException(e); }
                }

                _ = new LateTask(() =>
                {
                    for (var index = CustomNetObject.AllObjects.Count - 1; index >= 0; index--)
                    {
                        try { CustomNetObject.AllObjects[index]?.OnMeetingTasks(); }
                        catch (Exception e) { Utils.ThrowException(e); }
                    }
                }, 5f, "CNO OnMeeting");

                Alchemist.OnReportDeadBodyGlobal();

                if (Aware.IsEnable) Aware.OnReportDeadBody();

                Sleuth.OnReportDeadBody(player, target);
                Evader.ReportDeadBody();
            }, 0.15f, "AfterReportTasks_Part1");
        }
        catch (Exception error)
        {
            Utils.ThrowException(error);
            Logger.Error($"Error: {error}", "AfterReportTasks");
            Logger.SendInGame($"Error: {error}");
        }

        _ = new LateTask(() =>
        {
            foreach (var pc in Main.EnumeratePlayerControls())
            {
                pc.RpcRemoveAbilityCD();
                if (Main.Invisible.Contains(pc.PlayerId))
                {
                    pc.RpcMakeVisible();
                }

                if (Camouflage.WaitingChangeSkin.Contains(pc.PlayerId))
                {
                    Camouflage.RpcSetSkin(player);
                    Camouflage.WaitingChangeSkin.Remove(player.PlayerId);
                }

                if (!Main.OvverideOutfit.ContainsKey(pc.PlayerId))
                {

                    if (Camouflage.IsCamouflage && Options.KPDCamouflageMode.GetValue() is 2 or 3)
                    {
                        Camouflage.RpcSetSkin(pc);
                    }

                    if (Main.CheckShapeshift.ContainsKey(pc.PlayerId))
                    {
                        pc.RpcShapeshift(pc, false);
                        Camouflage.RpcSetSkin(pc, RevertToDefault: true);
                    }
                }

                if (GameStates.FungleIsActive && (pc.IsMushroomMixupActive() || Utils.IsActive(SystemTypes.MushroomMixupSabotage)))
                {
                    pc.FixMixedUpOutfit();
                }

                Logger.Info($"Player {pc?.Data?.PlayerName}: Id {pc.PlayerId} - is alive: {pc.IsAlive()}", "CheckIsAlive");
            }

            RPC.SyncDeadPassedMeetingList();

            MeetingTimeManager.OnReportDeadBody();

            NameNotifyManager.Reset();

            Utils.NotifyRoles(isForMeeting: true, CamouflageIsForMeeting: true);
        }, 0.15f, "AfterReportTasks_Part2");

        _ = new LateTask(Utils.SyncAllSettings, 3f, "Sync all settings after report");

        if (Options.UseMeetingAbilityMethod.GetValue() == 2 && Utils.IsActive(SystemTypes.Comms)) _ = new LateTask(FixComms, 3f, "Fix comms after report");
    }

    public static void FixComms()
    {
        BypassComms = true;
        var mapId = Utils.GetActiveMapId();

        ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Comms, 16);
        if (mapId is 1 or 5)
            ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Comms, 17);
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
class FixedUpdateInNormalGamePatch
{
    private static readonly StringBuilder RealName = new();
    private static readonly StringBuilder DeathReason = new();
    private static readonly StringBuilder Mark = new(20);
    private static readonly StringBuilder Suffix = new(120);
    public static readonly Dictionary<byte, int> BufferTime = [];
    public static readonly Dictionary<byte, int> TeleportBuffer = [];
    public static readonly Dictionary<byte, TMPro.TextMeshPro> RoleTextCache = [];
    private static int LevelKickBufferTime = 20;

    public static void Postfix(PlayerControl __instance)
    {
        if (__instance == null || __instance.PlayerId >= 254) return;

        CheckMurderPatch.Update(__instance.PlayerId);

        byte id = __instance.PlayerId;

        if (GameStates.IsHideNSeek) return;
        if (!GameStates.IsModHost) return;

        if (AmongUsClient.Instance.AmHost && GameStates.IsInTask && ReportDeadBodyPatch.CanReport[id] && ReportDeadBodyPatch.WaitReport[id].Count > 0)
        {
            if (Glitch.HasEnabled && Glitch.OnCheckFixedUpdateReport(id))
            {
                Glitch.CancelReportInFixedUpdate(__instance, id);
            }
            else
            {
                var info = ReportDeadBodyPatch.WaitReport[id][0];
                ReportDeadBodyPatch.WaitReport[id].Clear();
                Logger.Info($"{__instance.GetNameWithRole().RemoveHtmlTags()}: The report will be processed now that it is available for reporting", "ReportDeadbody");
                __instance.ReportDeadBody(info);
            }
        }

        try
        {
            DoPostfix(__instance);
        }
        catch (Exception ex)
        {
            if (OnGameJoinedPatch.JoiningGame && ex is NullReferenceException) return;

            var nameWithRole = __instance.GetNameWithRole();
            if (string.IsNullOrWhiteSpace(nameWithRole)) return;

            Logger.Error($"Error for {__instance.GetNameWithRole().RemoveHtmlTags()}: Error: {ex}", "FixedUpdateInNormalGamePatch");
        }
    }

    private static void DoPostfix(PlayerControl __instance)
    {

        var localPlayer = PlayerControl.LocalPlayer;
        byte localPlayerId = localPlayer.PlayerId;
        var player = __instance;
        if (player == null)
        {
            Logger.Warn("player was null", "FixedUpdateInNormalGamePatch");
            return;
        }
        byte playerId = player.PlayerId;

        var playerData = player.Data;
        var playerClientId = player.GetClientId();

        bool playerAmOwner = player.AmOwner;
        var amongUsClient = AmongUsClient.Instance;
        bool amHost = amongUsClient.AmHost;

        bool inGame = GameStates.IsInGame;
        bool inLobby = GameStates.IsLobby;
        bool isInTask = GameStates.IsInTask;

        var nowTime = Utils.TimeStamp;

        bool lowLoad = false;

        if (!BufferTime.TryGetValue(player.PlayerId, out var timerLowLoad))
        {
            BufferTime[player.PlayerId] = 30;
            timerLowLoad = 30;
        }

        timerLowLoad--;

        if (timerLowLoad > 0)
        {
            if (Options.LowLoadMode.GetBool())
                lowLoad = true;
        }
        else
        {
            timerLowLoad = 30;
        }

        BufferTime[player.PlayerId] = timerLowLoad;

        if (__instance.AmOwner && timerLowLoad == 30)
        {
            TeleportBuffer.Clear();
        }

        if (!lowLoad)
        {
            Zoom.OnFixedUpdate();
            TextBoxPatch.CheckChatOpen();

        }

        if (inGame)
        {
            Sniper.OnFixedUpdateGlobal(player);

            if (!lowLoad)
            {
                NameNotifyManager.OnFixedUpdate(player);
                TargetArrow.OnFixedUpdate(player);
                LocateArrow.OnFixedUpdate(player);
            }
        }

        if (amHost)
        {
            if (inLobby)
            {
                if (!lowLoad && !Main.DoBlockNameChange)
                    Utils.ApplySuffix(player);

                bool shouldChangeGamePublic = (ModUpdater.hasUpdate && ModUpdater.forceUpdate) || ModUpdater.isBroken || !Main.AllowPublicRoom || !VersionChecker.IsSupported;
                if (shouldChangeGamePublic && amongUsClient.IsGamePublic)
                {
                    amongUsClient.ChangeGamePublic(false);
                }

                bool playerInAllowList = false;
                if (Options.ApplyAllowList.GetBool())
                {
                    playerInAllowList = BanManager.CheckAllowList(playerData.FriendCode);
                }

                if (!playerInAllowList)
                {
                    bool shouldKickLowLevelPlayer = !lowLoad && !playerAmOwner && Options.KickLowLevelPlayer.GetInt() != 0 && playerData.PlayerLevel != 0 && playerData.PlayerLevel < Options.KickLowLevelPlayer.GetInt();

                    if (shouldKickLowLevelPlayer && --LevelKickBufferTime <= 0)
                    {
                        LevelKickBufferTime = 20;
                        var msg = new StringBuilder();
                        if (!Options.TempBanLowLevelPlayer.GetBool())
                        {
                            amongUsClient.KickPlayer(playerClientId, false);
                            msg.Append(string.Format(GetString("KickBecauseLowLevel"), player.GetRealName().RemoveHtmlTags()));
                            Logger.SendInGame(msg.ToString());
                            Logger.Info(msg.ToString(), "Low Level Kick");
                        }
                        else
                        {
                            var playerClient = player.GetClient();
                            if (playerClient.ProductUserId != "")
                            {
                                BanManager.TempBanWhiteList.Add(playerClient.GetHashedPuid());
                            }
                            msg.Append(string.Format(GetString("TempBannedBecauseLowLevel"), player.GetRealName().RemoveHtmlTags()));
                            Logger.SendInGame(msg.ToString());
                            amongUsClient.KickPlayer(playerClientId, true);
                            Logger.Info(msg.ToString(), "Low Level Temp Ban");
                        }
                    }
                }

                if (KickPlayerPatch.AttemptedKickPlayerList.Count > 0)
                {
                    foreach (var item in KickPlayerPatch.AttemptedKickPlayerList)
                    {
                        KickPlayerPatch.AttemptedKickPlayerList[item.Key]++;

                        if (item.Value > 11)
                            KickPlayerPatch.AttemptedKickPlayerList.Remove(item.Key);
                    }
                }
            }
            else if (inGame)
            {
                AFKDetector.OnFixedUpdate(player);
                DoubleTrigger.OnFixedUpdate(player);
                KillTimerManager.FixedUpdate(player);
                try { DataFlagRateLimiter.OnFixedUpdate(); }
                catch (Exception e) { Utils.ThrowException(e); }

                if (playerAmOwner)
                {
                    if (Options.LadderDeath.GetBool() && player.IsAlive())
                        FallFromLadder.FixedUpdate(player);

                    if (CustomNetObject.AllObjects.Count > 0)
                        CustomNetObject.FixedUpdate();

                    if (!lowLoad)
                    {
                        DisableDevice.FixedUpdate();

                        CovenManager.NecronomiconCheck();

                        if (Rainbow.IsEnabled && Main.IntroDestroyed)
                            Rainbow.OnFixedUpdate();
                    }
                }

                if (!lowLoad)
                {
                    if (Main.RefixCooldownDelay <= 0)
                    {
                        if (player.Is(CustomRoles.Vampire) || player.Is(CustomRoles.Warlock) || player.Is(CustomRoles.Ninja))
                            Main.AllPlayerKillCooldown[playerId] = Options.DefaultKillCooldown * 2;

                        else if (player.Is(CustomRoles.Poisoner))
                            Main.AllPlayerKillCooldown[playerId] = Poisoner.KillCooldown.GetFloat() * 2;
                    }

                    if (player.Is(CustomRoles.Mini))
                    {
                        if (!playerData.Disconnected)
                            Mini.OnFixedUpdates(player, nowTime);
                    }
                }

                if (isInTask && !AntiBlackout.SkipTasks)
                {
                    if (!lowLoad && player.HasAbilityCD() && player.IsAlive())
                    {
                        if (player.RemainingCD() <= 0)
                            player.RpcRemoveAbilityCD();

                        if (!player.IsModded() && player.RemainingCD() <= 60)
                            Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player, ForceLoop: false);
                    }
                    CustomRoleManager.OnFixedUpdate(player, lowLoad, nowTime, timerLowLoad);

                    player.OnFixedAddonUpdate(lowLoad);

                    if (GameModeBase.GetGameMode() == CustomGameMode.BonfireNight) BonfireNight.FixedUpdate();
                    if (GameModeBase.GetGameMode() == CustomGameMode.CopsAndRobbers) CopsAndRobbers.FixedUpdate();

                    if (!lowLoad && Main.AllPlayerSpeed.TryGetValue(playerId, out var speed))
                    {
                        if (!Main.LastAllPlayerSpeed.ContainsKey(playerId))
                        {
                            Main.LastAllPlayerSpeed[playerId] = speed;
                        }
                        else if (!Main.LastAllPlayerSpeed[playerId].Equals(speed))
                        {
                            Main.LastAllPlayerSpeed[playerId] = speed;
                            player.SyncSpeed();
                        }
                    }

                    if (Main.LateOutfits.TryGetValue(playerId, out var Method) && !player.CheckCamoflague())
                    {
                        Method();
                        Main.LateOutfits.Remove(playerId);
                        Logger.Info($"Reset {player.GetRealName()}'s outfit", "LateOutfits..OnFixedUpdate");
                    }

                    if (playerAmOwner)
                    {

                        if (player.CanUseKillButton() && !playerData.IsDead)
                        {
                            var players = player.GetPlayersInAbilityRangeSorted();
                            PlayerControl closest = !players.Any() ? null : players.First();
                            DestroyableSingleton<HudManager>.Instance.KillButton.SetTarget(closest);
                        }

                        if (timerLowLoad % 6 == 0)
                        {
                            PlayerGameOptionsSender.SendAllImmediately();

                        }
                    }
                }
            }
        }

        if (!RoleTextCache.TryGetValue(playerId, out var roleText))
        {
            var roleTextTransform = player.cosmetics.nameText.transform.Find("RoleText");
            roleText = roleTextTransform.GetComponent<TMPro.TextMeshPro>();
            RoleTextCache[playerId] = roleText;
        }

        if (lowLoad || roleText == null || player == null) return;

        if (inLobby)
        {
            var playerName = player.name;
            var moddedTag = new StringBuilder();

            if (Main.ShowModdedClientText.Value && Main.playerVersion.TryGetValue(playerClientId, out var ver))
            {
                if (Main.ForkId != ver.forkId)
                {
                    moddedTag.Append($"<color=#ff0000><size=1.4>{ver.forkId}</size>\n{playerName}</color>");
                }
                else if (Main.version.CompareTo(ver.version) == 0)
                {
                    string tag = ver.tag == $"{ThisAssembly.Git.Commit}({ThisAssembly.Git.Branch})"
                        ? $"<color=#fcc5f8><size=1.4>{GetString("ModdedClient")}</size>\n{playerName}</color>"
                        : $"<color=#ffff00><size=1.4>{ver.tag}</size>\n{playerName}</color>";
                    moddedTag.Append(tag);
                }
                else
                {
                    moddedTag.Append($"<color=#ff0000><size=1.4>v{ver.version}</size>\n{playerName}</color>");
                }
            }
            else if (Main.BAUPlayers.TryGetValue(playerData, out var puid) && puid == playerData.Puid)
            {
                moddedTag.Append($"<color=#0dff00>{playerName}</color>");
            }
            else
            {
                moddedTag.Append(playerData?.PlayerName);
            }

            player.cosmetics.nameText.text = moddedTag.ToString();
        }
        else if (inGame)
        {
            var localPlayerRole = localPlayer.GetCustomRole();
            var localPlayerRoleClass = localPlayer.GetRoleClass();

            var (text, color) = Utils.GetRoleAndSubText(localPlayerId, playerId, isMeeting: false);

            roleText.text = GameModeBase.GetGameMode() is CustomGameMode.FFA ? string.Empty : text;
            roleText.color = color;

            if (playerAmOwner || GameModeBase.GetGameMode() is CustomGameMode.FFA) roleText.enabled = true;
            else if (ExtendedPlayerControl.KnowRoleTarget(localPlayer, player)) roleText.enabled = true;
            else roleText.enabled = false;

            if (localPlayer.IsAlive() && Overseer.IsRevealedPlayer(localPlayer, player))
            {
                var blankRT = new StringBuilder();
                var result = new StringBuilder(roleText.text);
                if ((player.Is(CustomRoles.Trickster) && (!player.Is(CustomRoles.Narc) || localPlayer.Is(CustomRoles.Madmate))) || Illusionist.IsCovIllusioned(playerId))
                {
                    blankRT.Clear().Append(Overseer.GetRandomRole(localPlayerId));
                    blankRT.Append(TaskState.GetTaskState());
                    result.Clear().Append($"<size=1.3>{blankRT}</size>");
                }
                if (player.Is(CustomRoles.Narc) && !localPlayer.Is(CustomRoles.Madmate))
                {
                    blankRT.Clear().Append(CustomRoles.Sheriff.ToColoredString());
                    if (Sheriff.ShowShotLimit.GetBool()) blankRT.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Sheriff).ShadeColor(0.25f), $" ({Sheriff.ShotLimitOpt.GetInt()})"));
                    result.Clear().Append($"<size=1.3>{blankRT}</size>");
                }
                if (Illusionist.IsNonCovIllusioned(playerId))
                {
                    var randomRole = CustomRolesHelper.AllRoles.Where(role => role.IsEnable() && !role.IsAdditionRole() && role.IsCoven()).ToList().RandomElement();
                    blankRT.Clear().Append(randomRole.ToColoredString());
                    if (randomRole is CustomRoles.CovenLeader or CustomRoles.Jinx or CustomRoles.Illusionist or CustomRoles.VoodooMaster)
                    {
                        blankRT.Append(randomRole.GetStaticRoleClass().GetProgressText(localPlayerId, false));
                    }
                    result.Clear().Append($"<size=1.3>{blankRT}</size>");
                }
                roleText.text = result.ToString();
            }

            if (!amongUsClient.IsGameStarted && amongUsClient.NetworkMode != NetworkModes.FreePlay)
            {
                roleText.enabled = false;
                if (!playerAmOwner)
                    player.cosmetics.nameText.text = playerData?.PlayerName;
            }

            if (!roleText.isActiveAndEnabled && roleText.enabled)
                roleText.gameObject.SetActive(true);

            if (Main.VisibleTasksCount)
                roleText.text += Utils.GetProgressText(player);

            if (!playerAmOwner && localPlayer != DollMaster.DollMasterTarget)
                player = DollMaster.SwapPlayerInfo(player);

            RealName.Clear().Append(player.GetRealName());

            if (playerAmOwner && isInTask)
            {
                if (GameModeBase.GetGameMode() is CustomGameMode.FFA)
                {
                    string FFAName = string.Empty;
                    FFAManager.GetNameNotify(player, ref FFAName);
                    RealName.Clear().Append(FFAName);
                }
                else
                {
                    if (Pelican.IsEaten(localPlayerId))
                        RealName.Clear().Append(CustomRoles.Pelican.GetColoredTextByRole(GetString("EatenByPelican")));

                    else if (Deathpact.IsInActiveDeathpact(localPlayer))
                        RealName.Clear().Append(Deathpact.GetDeathpactString(localPlayer));
                }

                if (NameNotifyManager.GetNameNotify(player, out var name))
                    RealName.Clear().Append(name);
            }

            var oldRealName = new StringBuilder(RealName.ToString().ApplyNameColorData(localPlayer, player, false));
            RealName.Clear().Append(oldRealName);

            Mark.Clear();
            Suffix.Clear();

            if (player.CheckFirstDied() && MeetingStates.FirstMeeting)
            {
                if (Options.ShowShieldedPlayerToAll.GetBool() || localPlayerId == playerId)
                {
                    oldRealName.Clear().Append(RealName);
                    RealName.Clear().Append("<color=#4fa1ff><u></color>").Append(oldRealName).Append("</u>");
                    Mark.Append("<color=#4fa1ff>✚</color>");
                }
            }

            switch (GameModeBase.GetGameMode())
            {
                case CustomGameMode.FFA:
                    Suffix.Append(FFAManager.GetPlayerArrow(localPlayer, player));
                    break;

                default:
                    Mark.Append(localPlayerRoleClass?.GetMark(localPlayer, player, false));
                    Mark.Append(CustomRoleManager.GetMarkOthers(localPlayer, player, false));

                    Suffix.Append(CustomRoleManager.GetLowerTextOthers(localPlayer, player, false, false));

                    Suffix.Append(localPlayerRoleClass?.GetSuffix(localPlayer, player, false));
                    Suffix.Append(CustomRoleManager.GetSuffixOthers(localPlayer, player, false));

                    Suffix.Append(Radar.GetPlayerArrow(localPlayer, player, isForMeeting: false));
                    Suffix.Append(Necromancer.NecromancerReminder(localPlayer, player, isForMeeting: false));
                    Suffix.Append(CopyCat.CopycatReminder(localPlayer, player, isForMeeting: false));

                    if (localPlayerRole.IsImpostor() && player.GetPlayerTaskState().IsTaskFinished)
                    {
                        if (player.Is(CustomRoles.Snitch) && player.Is(CustomRoles.Madmate))
                            Mark.Append(CustomRoles.Impostor.GetColoredTextByRole("★"));
                    }
                    if ((localPlayer.IsPlayerCovenTeam() || !localPlayer.IsAlive()) && player.IsPlayerCovenTeam() && CovenManager.HasNecronomicon(player))
                    {
                        Mark.Append(CustomRoles.Coven.GetColoredTextByRole("♣"));
                    }

                    if (player.Is(CustomRoles.Cyber) && Cyber.CyberKnown.GetBool())
                        Mark.Append(CustomRoles.Cyber.GetColoredTextByRole("★"));

                    Mark.Append(Lovers.GetMarkOthers(localPlayer, player));

                    Mark.Append(Mini.GetMarkOthers(localPlayer, player));
                    break;
            }

            Suffix.Append(AFKDetector.GetSuffix(localPlayer, player));

            if (Devourer.HasEnabled)
            {
                bool targetDevoured = Devourer.HideNameOfTheDevoured(playerId);
                if (targetDevoured)
                {
                    RealName.Clear().Append(GetString("DevouredName"));
                }
            }

            if (DollMaster.HasEnabled && localPlayerRole != CustomRoles.DollMaster)
            {
                if (DollMaster.IsDoll(localPlayerId))
                    RealName.Clear().Append("<size=10000%><color=#000000>■</color></size>");
            }

            if ((Camouflage.IsActive && Utils.IsActive(SystemTypes.Comms)) || Camouflager.AbilityActivated)
            {
                oldRealName = RealName;
                RealName.Clear().Append($"<size=0%>{oldRealName}</size> ");
            }
            DeathReason.Clear().Append(localPlayer.Data.IsDead && localPlayer.KnowDeathReason(player)
                ? $"\n<size=1.7>『{CustomRoles.Doctor.GetColoredTextByRole(Utils.GetVitalText(playerId))}』</size>" : string.Empty);

            var currentText = player.cosmetics.nameText.text;
            var changeTo = $"{RealName}{DeathReason}{Mark}\r\n{Suffix}";
            bool needUpdate = currentText != changeTo;

            if (needUpdate)
            {
                player.cosmetics.nameText.text = changeTo;
                float offset = 0.2f;
                float colorBlind = -0.2f;

                if (NameNotifyManager.Notice.TryGetValue(localPlayerId, out var notifies) && notifies != null && notifies.Any())
                {
                    int notifyCount = notifies.Count;
                    for (int i = 0; i < notifyCount - 1; i++)
                    {
                        offset += 0.1f;
                        colorBlind -= 0.1f;
                    }
                }

                if (Suffix.Length > 0)
                {

                    offset += 0.15f;
                    colorBlind -= 0.2f;
                }
                if (!localPlayer.IsAlive() && !player.IsAlive()) { offset += 0.1f; colorBlind -= 0.1f; }

                roleText.transform.SetLocalY(offset);
                player.cosmetics.colorBlindText.transform.SetLocalY(colorBlind);
            }

        }
        else
        {
            roleText.transform.SetLocalY(0.2f);
            player.cosmetics.colorBlindText.transform.SetLocalY(-0.32f);
        }
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Start))]
class PlayerStartPatch
{
    public static void Postfix(PlayerControl __instance)
    {
        if (GameStates.IsHideNSeek) return;

        var roleText = Object.Instantiate(__instance.cosmetics.nameText);
        roleText.transform.SetParent(__instance.cosmetics.nameText.transform);
        roleText.fontMaterial.SetFloat("_StencilComp", 7f);
        roleText.fontMaterial.SetFloat("_Stencil", 2f);
        roleText.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        roleText.fontSize = 1.3f;
        roleText.text = "RoleText";
        roleText.gameObject.name = "RoleText";
        roleText.enabled = false;
    }

    public static Exception Finalizer()
    {
        return null;
    }
}

[HarmonyPatch]
class CoEnterVentPatch
{
    public static MethodBase TargetMethod()
    {
        return Utils.GetStateMachineMoveNext<PlayerPhysics>(nameof(PlayerPhysics._CoEnterVent_d__47));
    }

    public static bool Prefix(PlayerPhysics._CoEnterVent_d__47 __instance)
    {
        if (__instance.__1__state >= 1) return true;

        if (MeetingHud.Instance) return true;

        var instance = __instance.__4__this;
        var id = __instance.id;

        if (!AmongUsClient.Instance.AmHost || GameStates.IsHideNSeek) return true;
        Logger.Info($" {instance.myPlayer.GetNameWithRole().RemoveHtmlTags()}, Vent ID: {id}", "CoEnterVent");

        if (GameModeBase.GetGameMode() == CustomGameMode.FFA && FFAManager.CheckCoEnterVent(instance, id))
        {
            return true;
        }

        if (KillTimerManager.AllKillTimers.TryGetValue(instance.myPlayer.PlayerId, out var timer))
        {
            KillTimerManager.AllKillTimers[instance.myPlayer.PlayerId] = timer + 0.5f;
        }

        if (AntiBlackout.SkipTasks)
        {
            Logger.Warn($"AntiBlackout SkipTasks is enabled, {instance.myPlayer.GetNameWithRole().RemoveHtmlTags()} can't enter vent", "CoEnterVent");
            _ = new LateTask(() => instance?.RpcBootFromVent(id), 0.5f, "Prevent Enter Vents");
            return true;
        }

        if (CustomRoleManager.OthersCoEnterVent(instance, id))
        {
            return true;
        }

        var playerRoleClass = instance.myPlayer.GetRoleClass();

        if (!instance.myPlayer.CanUseVents() || (playerRoleClass != null && playerRoleClass.CheckBootFromVent(instance, id))
        )
        {
            _ = new LateTask(() => instance?.RpcBootFromVent(id), 0.5f, "Prevent Enter Vents");
            return true;
        }

        playerRoleClass?.OnCoEnterVent(instance, id);
        if (Options.DisableVenting1v1.GetBool() && Main.AllAlivePlayerControls.Count <= 2)
        {
            var pc = instance?.myPlayer;
            _ = new LateTask(() =>
            {
                pc?.Notify(GetString("FFA-NoVentingBecauseTwoPlayers"), 7f);
                pc?.MyPhysics?.RpcBootFromVent(id);
            }, 0.5f, "Player No Venting Because Two Players");
            return true;
        }

        if (playerRoleClass?.BlockMoveInVent(instance.myPlayer) ?? false)
        {
            foreach (var ventId in playerRoleClass.LastBlockedMoveInVentVents)
            {
                CustomRoleManager.BlockedVentsList[instance.myPlayer.PlayerId].Remove(ventId);
            }
            playerRoleClass.LastBlockedMoveInVentVents.Clear();

            var vent = ShipStatus.Instance.AllVents.First(v => v.Id == id);
            foreach (var nextvent in vent.NearbyVents.ToList())
            {
                if (nextvent == null) continue;

                if (nextvent.Id == id || (GameStates.DleksIsActive && id is 5 && nextvent.Id is 6)) continue;
                CustomRoleManager.BlockedVentsList[instance.myPlayer.PlayerId].Add(nextvent.Id);
                playerRoleClass.LastBlockedMoveInVentVents.Add(nextvent.Id);
            }
            instance.myPlayer.RpcSetVentInteraction();
        }

        _ = new LateTask(() => VentSystemDeterioratePatch.ForceUpadate = false, 1f, "Set Force Upadate As False", shoudLog: false);
        return true;
    }
}

[HarmonyPatch(typeof(Vent), nameof(Vent.EnterVent))]
class EnterVentPatch
{
    public static void Postfix(Vent __instance, [HarmonyArgument(0)] PlayerControl pc)
    {
        if (GameStates.IsHideNSeek) return;

        Main.LastEnteredVent.Remove(pc.PlayerId);
        Main.LastEnteredVent.Add(pc.PlayerId, __instance);
        Main.LastEnteredVentLocation.Remove(pc.PlayerId);
        Main.LastEnteredVentLocation.Add(pc.PlayerId, pc.GetCustomPosition());

        if (!AmongUsClient.Instance.AmHost || AntiBlackout.SkipTasks) return;

        if (TimeMaster.Rewinding)
        {
            pc.MyPhysics?.RpcExitVent(__instance.Id);
            return;
        }

        pc.GetRoleClass()?.OnEnterVent(pc, __instance);

        if (pc.Is(CustomRoles.Unlucky))
        {
            Unlucky.SuicideRand(pc, Unlucky.StateSuicide.EnterVent);
        }
    }
}

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.RpcEnterVent))]
class RpcEnterVentPatch
{
    public static bool Prefix(PlayerPhysics __instance, [HarmonyArgument(0)] int id)
    {

        if (AmongUsClient.Instance.AmClient)
        {
            __instance.StopAllCoroutines();
            __instance.StartCoroutine(__instance.CoEnterVent(id));
        }
        RpcEnterVentMessage rpcEnterVentMessage = new RpcEnterVentMessage(__instance.NetId, id);
        AmongUsClient.Instance.LateBroadcastReliableMessage(rpcEnterVentMessage.CastFast<IGameDataMessage>());

        return false;
    }
}

[HarmonyPatch]
class CoExitVentPatch
{
    public static MethodBase TargetMethod()
    {
        return Utils.GetStateMachineMoveNext<PlayerPhysics>(nameof(PlayerPhysics._CoExitVent_d__48));
    }

    public static void Postfix(PlayerPhysics._CoExitVent_d__48 __instance, ref bool __result)
    {
        if (__result) return;

        var instance = __instance.__4__this;
        var id = __instance.id;
        if (GameStates.IsHideNSeek) return;
        Logger.Info($" {instance.myPlayer.GetNameWithRole().RemoveHtmlTags()}, Vent ID: {id}", "CoExitVent");

        var player = instance.myPlayer;
        if (GameModeBase.GetGameMode() == CustomGameMode.FFA && FFAManager.FFA_DisableVentingWhenKCDIsUp.GetBool())
        {
            FFAManager.CoExitVent(player);
        }

        if (!AmongUsClient.Instance.AmHost) return;

        if (Camouflage.WaitingChangeSkin.Contains(player.PlayerId))
        {
            Camouflage.RpcSetSkin(player);
            Camouflage.WaitingChangeSkin.Remove(player.PlayerId);
        }

        player.GetRoleClass()?.OnExitVent(player, id);

        foreach (var ventId in player.GetRoleClass().LastBlockedMoveInVentVents)
        {
            CustomRoleManager.BlockedVentsList[player.PlayerId].Remove(ventId);
        }
        player.GetRoleClass().LastBlockedMoveInVentVents.Clear();

        _ = new LateTask(() => { player?.RpcSetVentInteraction(); }, 0.8f, $"Set vent interaction after exit vent {player?.PlayerId}", shoudLog: false);
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CompleteTask))]
class PlayerControlCompleteTaskPatch
{
    public static bool Prefix(PlayerControl __instance, uint idx)
    {
        if (GameStates.IsHideNSeek) return true;

        var player = __instance;
        var playerTask = player.myTasks?.ToArray().FirstOrDefault(task => task.Id == idx);
        var taskType = playerTask != null ? playerTask.TaskType : TaskTypes.None;

        Logger.Info($"Task Complete: {player.GetNameWithRole()} - Task id: {idx} Type: {taskType}", "CompleteTask.Prefix");
        var taskState = player.GetPlayerTaskState();
        taskState.Update(player);

        var ret = true;

        if (AmongUsClient.Instance.AmHost)
        {
            var roleClass = player.GetRoleClass();

            if (roleClass != null)
            {
                ret = roleClass.OnTaskComplete(player, taskState.CompletedTasksCount, taskState.AllTasksCount);
            }

            var playerIsOverridden = false;
            if (TaskManager.HasEnabled && TaskManager.GetTaskManager(player.PlayerId, out byte taskManagerId))
            {
                var taskManager = taskManagerId.GetPlayer();

                if (taskManager.IsAlive())
                {

                    player = taskManagerId.GetPlayer();
                    playerTask = player.myTasks?.ToArray().FirstOrDefault(task => task.Id == idx);
                    playerIsOverridden = true;
                }
                else
                {
                    TaskManager.ClearData(player.PlayerId);
                }
            }

            if (playerTask != null)
                CustomRoleManager.OthersCompleteThisTask(player, playerTask, playerIsOverridden, __instance);

            if (playerIsOverridden)
            {
                player = __instance;
                TaskManager.ClearData(player.PlayerId);
                Logger.Info($"playerId: {player.PlayerId} - __instanceId {__instance.PlayerId}", "CompleteTask.Prefix Finish");
            }

            var playerSubRoles = player.GetCustomSubRoles();

            if (playerSubRoles.Any())
            {
                foreach (var subRole in playerSubRoles)
                {
                    switch (subRole)
                    {
                        case CustomRoles.Unlucky when player.IsAlive():
                            Unlucky.SuicideRand(player, Unlucky.StateSuicide.CompleteTask);
                            break;

                        case CustomRoles.Tired when player.IsAlive():
                            Tired.AfterActionTasks(player);
                            break;

                        case CustomRoles.Bloodthirst when player.IsAlive():
                            Bloodthirst.OnTaskComplete(player);
                            break;

                        case CustomRoles.Ghoul when taskState.CompletedTasksCount >= taskState.AllTasksCount:
                            Ghoul.OnTaskComplete(player);
                            break;

                        case CustomRoles.Madmate when taskState.IsTaskFinished && player.Is(CustomRoles.Snitch):
                            foreach (var impostor in Main.EnumerateAlivePlayerControls().Where(pc => pc.Is(Custom_Team.Impostor) && !Main.PlayerStates[pc.PlayerId].IsNecromancer).ToArray())
                            {
                                NameColorManager.Add(impostor.PlayerId, player.PlayerId, "#ff1919");
                            }
                            break;
                    }
                }
            }
        }

        ret &= Workhorse.OnAddTask(player);

        Utils.NotifyRoles(SpecifySeer: player);
        Utils.NotifyRoles(SpecifyTarget: player);

        return ret;
    }
    public static void Postfix()
    {
        if (GameStates.IsHideNSeek) return;

        GameData.Instance.RecomputeTaskCounts();
        Logger.Info($"TotalTaskCounts = {GameData.Instance.CompletedTasks}/{GameData.Instance.TotalTasks}", "CompleteTask.Postfix");
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckName))]
class PlayerControlCheckNamePatch
{
    public static void Postfix(PlayerControl __instance, ref string playerName)
    {
        if (!AmongUsClient.Instance.AmHost || !GameStates.IsLobby) return;

        playerName = __instance.Data.PlayerName ?? playerName;

        if (BanManager.CheckDenyNamePlayer(__instance, playerName)) return;

        if (!Main.AllClientRealNames.ContainsKey(__instance.OwnerId))
        {
            Main.AllClientRealNames.Add(__instance.OwnerId, playerName);
        }

        var name = playerName;
        if (Options.FormatNameMode.GetInt() == 2 && __instance.Data.ClientId != AmongUsClient.Instance.ClientId)
            name = Main.Get_TName_Snacks;
        else
        {
            name = name.RemoveHtmlTags().Replace(@"\", string.Empty).Replace("/", string.Empty).Replace("\n", string.Empty).Replace("\r", string.Empty).Replace("<", string.Empty).Replace(">", string.Empty);
            if (name.Length > 10) name = name[..10];
            if (Options.DisableEmojiName.GetBool()) name = Regex.Replace(name, @"\p{Cs}", string.Empty);
            if (Regex.Replace(Regex.Replace(name, @"\s", string.Empty), @"[\x01-\x1F,\x7F]", string.Empty).Length < 1) name = Main.Get_TName_Snacks;
        }
        Main.AllPlayerNames.Remove(__instance.PlayerId);
        Main.AllPlayerNames.TryAdd(__instance.PlayerId, name);

        Logger.Info($"PlayerId: {__instance.PlayerId} - playerName: {playerName} => {name}", "Name player");

        RPC.SyncAllPlayerNames();
        if (__instance != null && !name.Equals(playerName))
        {
            Logger.Warn($"Standard nickname: {playerName} => {name}", "Name Format");
            __instance.RpcSetName(name);
        }

        _ = new LateTask(() =>
        {
            if (__instance != null && !__instance.Data.Disconnected && !__instance.IsModded())
            {
                var message = new RpcRequestRetryVersionCheck(PlayerControl.LocalPlayer.NetId);
                RpcUtils.LateSpecificSendMessage(message, __instance.OwnerId);
            }

            var sender = CustomRpcSender.Create("LobbyTagsSender", SendOption.Reliable);
            foreach (var player in Main.EnumeratePlayerControls())
            {
                if (player == null || player.PlayerId == __instance.PlayerId || player.Data == null || player.Data.Disconnected) continue;

                Main.AllClientRealNames.TryGetValue(player.OwnerId, out var rName);

                if (rName != null && rName != player.Data.PlayerName)
                {
                    sender.AutoStartRpc(player.NetId, (byte)RpcCalls.SetName, __instance.OwnerId);
                    sender.Write(player.Data.NetId);
                    sender.Write(player.Data.PlayerName);
                    sender.EndRpc();
                }

                if (sender.Length > 800)
                {
                    sender.SendMessage();
                    sender = CustomRpcSender.Create("LobbyTagsSenderSub", SendOption.Reliable);
                }
            }
            sender.SendMessage();

        }, 0.6f, "Retry Version Check", false);
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.SetColor))]
class RpcSetColorPatch
{
    public static void Postfix(PlayerControl __instance, byte bodyColor)
    {
        if (Main.IntroDestroyed || __instance == null) return;

        Logger.Info($"PlayerId: {__instance.PlayerId} - playerColor: {bodyColor}", "RpcSetColor");
        if (bodyColor == 255) return;

        Main.PlayerColors.Remove(__instance.PlayerId);
        Main.PlayerColors[__instance.PlayerId] = Palette.PlayerColors[bodyColor];
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdCheckName))]
class CmdCheckNameVersionCheckPatch
{
    public static void Postfix()
    {
        RPC.RpcVersionCheck();
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.ProtectPlayer))]
class PlayerControlProtectPlayerPatch
{
    public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
    {
        Logger.Info($"{__instance.GetNameWithRole().RemoveHtmlTags()} => {target.GetNameWithRole().RemoveHtmlTags()}", "ProtectPlayer");
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RemoveProtection))]
class PlayerControlRemoveProtectionPatch
{
    public static void Postfix(PlayerControl __instance)
    {
        Logger.Info($"{__instance.GetNameWithRole().RemoveHtmlTags()}", "RemoveProtection");
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MixUpOutfit))]
public static class PlayerControlMixupOutfitPatch
{
    public static void Postfix(PlayerControl __instance)
    {
        if (!__instance.IsAlive())
        {
            return;
        }

        if ((!PlayerControl.LocalPlayer.Is(Custom_Team.Impostor)
            || Main.PlayerStates[PlayerControl.LocalPlayer.PlayerId].IsNecromancer
            ) &&
            PlayerControl.LocalPlayer.HasDesyncRole())
        {

            __instance.cosmetics.ToggleNameVisible(false);
        }
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixMixedUpOutfit))]
public static class PlayerControlFixMixedUpOutfitPatch
{
    public static void Postfix(PlayerControl __instance)
    {
        if (!__instance.IsAlive())
        {
            return;
        }

        __instance.cosmetics.ToggleNameVisible(true);
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckSporeTrigger))]
public static class PlayerControlCheckSporeTriggerPatch
{
    public static bool Prefix()
    {
        if (AmongUsClient.Instance.AmHost)
        {
            return !Options.DisableSporeTriggerOnFungle.GetBool();
        }

        return true;
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckUseZipline))]
public static class PlayerControlCheckUseZiplinePatch
{
    public static bool Prefix([HarmonyArgument(2)] bool fromTop)
    {
        if (AmongUsClient.Instance.AmHost && Options.DisableZiplineOnFungle.GetBool())
        {
            if (Options.DisableZiplineFromTop.GetBool() && fromTop) return false;
            if (Options.DisableZiplineFromUnder.GetBool() && !fromTop) return false;
        }

        return true;
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Die))]
public static class PlayerControlDiePatch
{
    public static void Postfix(PlayerControl __instance, DeathReason reason)
    {
        if (!AmongUsClient.Instance.AmHost || __instance == null) return;
        var playerId = __instance.PlayerId;

        if (AntiBlackout.SkipTasks && AntiBlackout.ExilePlayerId != playerId) return;

        if (reason is DeathReason.Exile)
        {
            var securityCameraSystem = ShipStatus.Instance.Systems.TryGetValue(SystemTypes.Security, out var systemType) ? systemType.CastFast<SecurityCameraSystemType>() : null;
            if (securityCameraSystem != null && securityCameraSystem.PlayersUsing.Contains(playerId))
            {
                securityCameraSystem.PlayersUsing.Remove(playerId);
                securityCameraSystem.IsDirty = true;
            }
        }

        __instance.RpcRemovePet();

        if (!Main.LIMap) return;

        _ = new LateTask(() =>
        {
            if (Main.PlayerStates.TryGetValue(__instance.PlayerId, out var state) && !state.IsDead)
            {
                state.IsDead = true;
                var sender = CustomRpcSender.Create($"LIDeathSync:{__instance.GetRealName()}", SendOption.Reliable);
                var hasValue = __instance.SyncGeneralOptions();
                sender.SendMessage(dispose: !hasValue);
            }
        }, 0.2f);
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Revive))]
static class PlayerControlRevivePatch
{
    public static void Postfix(PlayerControl __instance)
    {
        if (!AmongUsClient.Instance.AmHost || !Main.LIMap) return;

        _ = new LateTask(() =>
        {
            if (Main.PlayerStates.TryGetValue(__instance.PlayerId, out var state) && state.IsDead)
            {
                state.SetAlive();
                var sender = CustomRpcSender.Create($"LIReviveSync:{__instance.GetRealName()}", SendOption.Reliable);
                var hasValue = __instance.SyncGeneralOptions();
                sender.SendMessage(dispose: !hasValue);
                Vector2 pos = __instance.GetCustomPosition();
                __instance.RpcTeleport(Main.EnumerateAlivePlayerControls().Without(__instance).Select(x => x.GetCustomPosition()).Concat(ShipStatus.Instance.AllVents.Select(x => new Vector2(x.transform.position.x, x.transform.position.y + 0.3636f))).MinBy(x => Vector2.Distance(pos, x)));
            }
        }, 0.2f);
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSetRole))]
class PlayerControlSetRolePatch
{
    private static readonly Dictionary<PlayerControl, RoleTypes> GhostRoles = [];
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] ref RoleTypes roleType, [HarmonyArgument(1)] ref bool canOverrideRole)
    {

        if (RpcSetRoleReplacer.BlockSetRole || GameStates.IsHideNSeek) return true;

        canOverrideRole = true;
        if (GameStates.IsHideNSeek || __instance == null) return true;
        if (!ShipStatus.Instance.enabled || !AmongUsClient.Instance.AmHost) return true;

        var target = __instance;
        var targetName = __instance.GetNameWithRole().RemoveHtmlTags();

        if (roleType is RoleTypes.CrewmateGhost or RoleTypes.ImpostorGhost)
        {
            try
            {
                GhostRoleAssign.GhostAssignPatch(__instance);
                __instance.SyncSettings();
            }
            catch (Exception error)
            {
                Logger.Warn($"Error After RpcSetRole: {error}", "RpcSetRole.Prefix.GhostAssignPatch");
            }

            var targetIsKiller = target.Is(Custom_Team.Impostor) || target.HasDesyncRole();
            GhostRoles.Clear();

            foreach (var seer in Main.EnumeratePlayerControls())
            {
                var self = seer.PlayerId == target.PlayerId;
                var seerIsKiller = seer.Is(Custom_Team.Impostor) || seer.HasDesyncRole();

                if (target.HasGhostRole())
                {
                    GhostRoles[seer] = target.GetCustomRole().GetRoleTypes();
                }
                else if ((self && targetIsKiller) || (!seerIsKiller && target.Is(Custom_Team.Impostor)))
                {
                    GhostRoles[seer] = RoleTypes.ImpostorGhost;
                }
                else
                {
                    GhostRoles[seer] = RoleTypes.CrewmateGhost;
                }
            }

            if (GhostRoles.All(kvp => kvp.Value == RoleTypes.SpiritGuide))
            {
                roleType = RoleTypes.SpiritGuide;
                __instance.RpcSetRoleDesync(RoleTypes.SpiritGuide, __instance.GetClientId());
                foreach (var seer in Main.EnumeratePlayerControls())
                {
                    if (seer.PlayerId == __instance.PlayerId) continue;
                    __instance.RpcSetRoleDesync(RoleTypes.CrewmateGhost, seer.GetClientId());
                }
                GhostRoleAssign.CreateGAMessage(__instance);
                return false;
            }

            if (GhostRoles.All(kvp => kvp.Value == RoleTypes.GuardianAngel))
            {
                roleType = RoleTypes.GuardianAngel;
                __instance.RpcSetRoleDesync(RoleTypes.GuardianAngel, __instance.GetClientId());
                foreach (var seer in Main.EnumeratePlayerControls())
                {
                    if (seer.PlayerId == __instance.PlayerId) continue;
                    __instance.RpcSetRoleDesync(RoleTypes.CrewmateGhost, seer.GetClientId());
                }
                GhostRoleAssign.CreateGAMessage(__instance);
                return false;
            }

            else if (GhostRoles.All(kvp => kvp.Value == RoleTypes.CrewmateGhost))
            {
                roleType = RoleTypes.CrewmateGhost;
                return true;
            }

            else if (GhostRoles.All(kvp => kvp.Value == RoleTypes.ImpostorGhost))
            {
                roleType = RoleTypes.ImpostorGhost;
                return true;
            }
            else
            {
                foreach ((var seer, var role) in GhostRoles)
                {
                    if (seer == null || target == null) continue;
                    Logger.Info($"Desync {targetName} => {role} for {seer.GetNameWithRole().RemoveHtmlTags()}", "PlayerControl.RpcSetRole");
                    target.RpcSetRoleDesync(role, seer.GetClientId());
                }
                return false;
            }
        }

        return true;
    }
    public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] RoleTypes roleType, bool __runOriginal)
    {
        if (!AmongUsClient.Instance.AmHost || __instance == null) return;

        try
        {
            if (__runOriginal)
            {
                Logger.Info($" {__instance.GetRealName()} => {roleType}", "PlayerControl.RpcSetRole");
            }
        }
        catch (Exception e)
        {
            Logger.Error($"Role Type:{roleType} - Run Original:{__runOriginal} - Error: {e}", "RpcSetRole.Postfix");
        }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CoSetRole))]
class PlayerControlLocalSetRolePatch
{
    public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] RoleTypes role)
    {
        Logger.Info($"CoSetRole: {role} for {__instance.GetRealName()}", "PlayerControl.CoSetRole");

        if (!AmongUsClient.Instance.AmHost && GameStates.IsNormalGame && !GameStates.IsModHost)
        {
            var modRole = role switch
            {
                RoleTypes.Crewmate => CustomRoles.CrewmateTONE,
                RoleTypes.Impostor => CustomRoles.ImpostorTONE,
                RoleTypes.Scientist => CustomRoles.ScientistTONE,
                RoleTypes.Engineer => CustomRoles.EngineerTONE,
                RoleTypes.Shapeshifter => CustomRoles.ShapeshifterTONE,
                RoleTypes.Noisemaker => CustomRoles.NoisemakerTONE,
                RoleTypes.Phantom => CustomRoles.PhantomTONE,
                RoleTypes.Tracker => CustomRoles.TrackerTONE,
                RoleTypes.Detective => CustomRoles.DetectiveTONE,
                RoleTypes.Viper => CustomRoles.ViperTONE,
                RoleTypes.Judge => CustomRoles.JudgeTONE,
                RoleTypes.SpiritGuide => CustomRoles.InfluencerTONE,
                _ => CustomRoles.NotAssigned,
            };
            if (modRole != CustomRoles.NotAssigned)
            {
                Main.PlayerStates[__instance.PlayerId].SetMainRole(modRole);
            }
        }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.AssertWithTimeout))]
class AssertWithTimeoutPatch
{

    public static bool Prefix()
    {
        return false;
    }
}
