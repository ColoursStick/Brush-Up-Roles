using AmongUs.Data;
using System;
using TONE.Roles.AddOns.Common;
using TONE.Roles.Core;
using TONE.Roles.Neutral;

namespace TONE;

class ExileControllerWrapUpPatch
{
    public static NetworkedPlayerInfo AntiBlackout_LastExiled;
    [HarmonyPatch(typeof(ExileController), nameof(ExileController.Begin))]
    class ExileControllerBeginPatch
    {

        public static void Postfix(ExileController __instance, [HarmonyArgument(0)] ExileController.InitProperties init)
        {
            if (GameModeBase.GetGameMode() is CustomGameMode.Standard && init != null && init.outfit != null && (AmongUsClient.Instance.AmHost || !Main.CurrentServerIsVanilla))
                __instance.completeString = CheckForEndVotingPatch.TempExileMsg;

        }
    }

    [HarmonyPatch(typeof(ExileController), nameof(ExileController.WrapUp))]
    class BaseExileControllerPatch
    {
        public static void Prefix()
        {
            CheckAndDoRandomSpawn();
            CheckForEndVotingPatch.TempExiledPlayer = null;
        }
        public static void Postfix(ExileController __instance)
        {
            if (Main.LIMap) return;

            try
            {
                WrapUpPostfix(__instance.initData.networkedPlayer);
            }
            catch (Exception error)
            {
                Logger.Error($"Error after exiled: {error}", "WrapUp");
            }
            finally
            {
                WrapUpFinalizer(__instance.initData.networkedPlayer);
            }
        }
    }

    [HarmonyPatch]
    class AirshipExileControllerPatch
    {
        public static MethodBase TargetMethod()
        {
            return Utils.GetStateMachineMoveNext<AirshipExileController>(nameof(AirshipExileController._WrapUpAndSpawn_d__11));
        }

        public static void Postfix(AirshipExileController._WrapUpAndSpawn_d__11 __instance, ref bool __result)
        {
            if (Main.LIMap) return;

            var instance = __instance.__4__this;
            if (!__result)
            {
                Logger.Info("AirshipExileController WrapUpAndSpawn Postfix", "AirshipExileControllerPatch");
                try
                {
                    WrapUpPostfix(instance.initData.networkedPlayer);
                }
                catch (Exception error)
                {
                    Logger.Error($"Error after exiled: {error}", "WrapUpAndSpawn");
                }
                finally
                {
                    WrapUpFinalizer(instance.initData.networkedPlayer);
                }
            }
        }
    }
    private static void CheckAndDoRandomSpawn()
    {
        if (!AmongUsClient.Instance.AmHost || Main.LIMap) return;
        if (RandomSpawn.IsRandomSpawn() || GameModeBase.GetGameMode() == CustomGameMode.FFA)
        {
            RandomSpawn.SpawnMap spawnMap = Utils.GetActiveMapName() switch
            {
                MapNames.Skeld => new RandomSpawn.SkeldSpawnMap(),
                MapNames.MiraHQ => new RandomSpawn.MiraHQSpawnMap(),
                MapNames.Polus => new RandomSpawn.PolusSpawnMap(),
                MapNames.Dleks => new RandomSpawn.DleksSpawnMap(),
                MapNames.Fungle => new RandomSpawn.FungleSpawnMap(),
                _ => null,
            };
            if (spawnMap != null) Main.EnumeratePlayerControls().Do(spawnMap.RandomTeleport);
        }
    }
    public static void WrapUpPostfix(NetworkedPlayerInfo exiled)
    {
        if (AntiBlackout.BlackOutIsActive) exiled = AntiBlackout_LastExiled;

        if (!GameStates.AirshipIsActive)
        {
            foreach (var state in Main.PlayerStates.Values)
            {
                state.HasSpawned = true;
            }
        }

        bool DecidedWinner = false;
        if (!AmongUsClient.Instance.AmHost) return;
        AntiBlackout.RestoreIsDead(doSend: false);

        List<Collector> collectorCL = Utils.GetRoleBasesByType<Collector>()?.ToList();

        if (collectorCL != null) Logger.Info($"{!collectorCL.Any(x => x.CollectorWin(false))}", "!Collector.CollectorWin(false)");
        Logger.Info($"{exiled != null}", "exiled != null");
        bool CLThingy = collectorCL == null || !collectorCL.Any(x => x.CollectorWin(false));

        if (CLThingy && exiled != null)
        {
            exiled.IsDead = true;
            exiled.PlayerId.SetDeathReason(PlayerState.DeathReason.Vote);

            var exiledRoleClass = exiled.PlayerId.GetRoleClassById();
            var emptyString = string.Empty;

            exiledRoleClass?.CheckExile(exiled, ref DecidedWinner, isMeetingHud: false, name: ref emptyString);
            if (exiled.Object.Is(CustomRoles.Mini)) Mini.CheckExile(exiled, ref DecidedWinner, isMeetingHud: false, name: ref emptyString);
            CustomRoleManager.AllEnabledRoles.Do(roleClass => roleClass.CheckExileTarget(exiled, ref DecidedWinner, isMeetingHud: false, name: ref emptyString));

            if (CustomWinnerHolder.WinnerTeam != CustomWinner.Terrorist) Main.PlayerStates[exiled.PlayerId].SetDead();
        }

        if (AmongUsClient.Instance.AmHost && Main.IsFixedCooldown)
        {
            Main.RefixCooldownDelay = Options.DefaultKillCooldown - 3f;
        }

        foreach (var player in Main.EnumeratePlayerControls())
        {
            player.GetRoleClass()?.OnPlayerExiled(player, exiled);

            player.RpcRemovePet();

            player.DoUnShiftState();
        }

        Main.MeetingIsStarted = false;
        Main.MeetingsPassed++;

        Utils.CountAlivePlayers(sendLog: true, checkGameEnd: GameModeBase.GetGameMode() is CustomGameMode.Standard);
    }

    public static void WrapUpFinalizer(NetworkedPlayerInfo exiled)
    {

        if (AmongUsClient.Instance.AmHost)
        {
            _ = new LateTask(() =>
            {
                if (GameStates.IsEnded) return;

                exiled = AntiBlackout_LastExiled;
                AntiBlackout.SendGameData();
                AntiBlackout.SetRealPlayerRoles();

                if ((AntiBlackout.BlackOutIsActive || Main.LIMap) &&
                    exiled != null &&
                    exiled.Object != null)
                {
                    exiled.Object.RpcExileV2();
                    exiled.Object.SetDeathReason(PlayerState.DeathReason.Vote);
                }
            }, GameModeBase.GetGameMode() is CustomGameMode.Standard ? 0.5f : 1.4f, "Restore IsDead Task");

            _ = new LateTask(AntiBlackout.ResetAfterMeeting, 0.6f, "ResetAfterMeeting");

            _ = new LateTask(() =>
            {
                if (GameStates.IsEnded) return;

                Main.PlayerStates.Values.DoIf(x => x.IsBlackOut, x =>
                {
                    x.IsBlackOut = false;
                });

                Main.AfterMeetingDeathPlayers.Do(x =>
                {
                    var player = x.Key.GetPlayer();
                    var state = Main.PlayerStates[x.Key];

                    Logger.Info($"{player?.GetNameWithRole().RemoveHtmlTags()} died with {x.Value}", "AfterMeetingDeath");

                    state.deathReason = x.Value;
                    player?.RpcExileV3();

                    _ = new LateTask(() => player?.RpcExile(), 0.5f, "Extra Exile to be Sure");

                    if (x.Value == PlayerState.DeathReason.Suicide)
                        player?.SetRealKiller(player, true);
                });

                Main.AfterMeetingDeathPlayers.Clear();

                Utils.AfterMeetingTasks();
                Utils.SyncAllSettings();
                Utils.CheckAndSetVentInteractions();

                if (Main.CurrentServerIsVanilla && Options.BypassRateLimitAC.GetBool())
                {
                    Main.Instance.StartCoroutine(Utils.NotifyEveryoneAsync());
                }
                else
                {
                    Utils.NotifyRoles();
                }

                Main.LastMeetingEnded = Utils.TimeStamp;
            }, 1f, "AfterMeetingDeathPlayers Task");
        }

        GameStates.AlreadyDied |= !Utils.IsAllAlive;
        RemoveDisableDevicesPatch.UpdateDisableDevices();
        SoundManager.Instance.ChangeAmbienceVolume(DataManager.Settings.Audio.AmbienceVolume);

        _ = new LateTask(() =>
        {
            if (!AmongUsClient.Instance.IsGameOver)
                DestroyableSingleton<HudManager>.Instance.SetHudActive(true);
        }, 0.8f, "Set Hud Active");

        Logger.Info("Start of Task Phase", "Phase");
    }

    [HarmonyPatch(typeof(PbExileController), nameof(PbExileController.PlayerSpin))]
    class PolusExileHatFixPatch
    {
        public static void Prefix(PbExileController __instance)
        {
            __instance.Player.cosmetics.hat.transform.localPosition = new(-0.2f, 0.6f, 1.1f);
        }
    }
}
