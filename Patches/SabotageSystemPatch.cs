using AmongUs.GameOptions;
using Hazel;
using TONE.Roles.AddOns.Common;
using TONE.Roles.Core;
using TONE.Roles.Crewmate;
using TONE.Roles.Impostor;

namespace TONE;

public class SabotageSystemPatch
{
    private static bool SetDurationForReactorSabotage = true;
    private static bool SetDurationForO2Sabotage = true;
    private static bool SetDurationMushroomMixupSabotage = true;

    [HarmonyPatch(typeof(ReactorSystemType), nameof(ReactorSystemType.Deteriorate))]
    private static class ReactorSystemTypePatch
    {
        private static void Prefix(ReactorSystemType __instance)
        {
            if (!Options.SabotageTimeControl.GetBool()) return;
            if (GameStates.AirshipIsActive) return;

            if (!__instance.IsActive || !SetDurationForReactorSabotage)
            {
                if (!SetDurationForReactorSabotage && !__instance.IsActive)
                {
                    SetDurationForReactorSabotage = true;
                }
                return;
            }

            Logger.Info($" {ShipStatus.Instance.Type}", "ReactorSystemTypePatch - ShipStatus.Instance.Type");
            Logger.Info($" {SetDurationForReactorSabotage}", "ReactorSystemTypePatch - SetDurationCriticalSabotage");
            SetDurationForReactorSabotage = false;

            switch (ShipStatus.Instance.Type)
            {
                case ShipStatus.MapType.Ship:
                    __instance.Countdown = Options.SkeldReactorTimeLimit.GetFloat();
                    return;
                case ShipStatus.MapType.Hq:
                    __instance.Countdown = Options.MiraReactorTimeLimit.GetFloat();
                    return;
                case ShipStatus.MapType.Pb:
                    __instance.Countdown = Options.PolusReactorTimeLimit.GetFloat();
                    return;
                case ShipStatus.MapType.Fungle:
                    __instance.Countdown = Options.FungleReactorTimeLimit.GetFloat();
                    return;
                default:
                    return;
            }
        }
    }
    [HarmonyPatch(typeof(HeliSabotageSystem), nameof(HeliSabotageSystem.Deteriorate))]
    private static class HeliSabotageSystemPatch
    {
        private static void Prefix(HeliSabotageSystem __instance)
        {
            if (!Options.SabotageTimeControl.GetBool()) return;
            if (!GameStates.AirshipIsActive) return;

            if (!__instance.IsActive || ShipStatus.Instance == null || !SetDurationForReactorSabotage)
            {
                if (!SetDurationForReactorSabotage && !__instance.IsActive)
                {
                    SetDurationForReactorSabotage = true;
                }
                return;
            }

            Logger.Info($" {ShipStatus.Instance.Type}", "HeliSabotageSystemPatch - ShipStatus.Instance.Type");
            Logger.Info($" {SetDurationForReactorSabotage}", "HeliSabotageSystemPatch - SetDurationCriticalSabotage");
            SetDurationForReactorSabotage = false;

            __instance.Countdown = Options.AirshipReactorTimeLimit.GetFloat();
        }
    }
    [HarmonyPatch(typeof(LifeSuppSystemType), nameof(LifeSuppSystemType.Deteriorate))]
    private static class LifeSuppSystemTypePatch
    {
        private static void Prefix(LifeSuppSystemType __instance)
        {
            if (!Options.SabotageTimeControl.GetBool()) return;
            if (Utils.GetActiveMapName() is MapNames.Polus or MapNames.Airship or MapNames.Fungle) return;

            if (!__instance.IsActive || !SetDurationForO2Sabotage)
            {
                if (!SetDurationForO2Sabotage && !__instance.IsActive)
                {
                    SetDurationForO2Sabotage = true;
                }
                return;
            }

            Logger.Info($" {ShipStatus.Instance.Type}", "LifeSuppSystemType - ShipStatus.Instance.Type");
            Logger.Info($" {SetDurationForO2Sabotage}", "LifeSuppSystemType - SetDurationCriticalSabotage");
            SetDurationForO2Sabotage = false;

            switch (ShipStatus.Instance.Type)
            {
                case ShipStatus.MapType.Ship:
                    __instance.Countdown = Options.SkeldO2TimeLimit.GetFloat();
                    return;
                case ShipStatus.MapType.Hq:
                    __instance.Countdown = Options.MiraO2TimeLimit.GetFloat();
                    return;
                default:
                    return;
            }
        }
    }
    [HarmonyPatch(typeof(MushroomMixupSabotageSystem), nameof(MushroomMixupSabotageSystem.UpdateSystem))]
    public static class MushroomMixupSabotageSystemUpdateSystemPatch
    {
        public static void Postfix()
        {
            Logger.Info($" IsActive", "MushroomMixupSabotageSystem.UpdateSystem.Postfix");

            foreach (var pc in Main.EnumerateAlivePlayerControls())
            {
                if ((!pc.Is(Custom_Team.Impostor) || Main.PlayerStates[pc.PlayerId].IsNecromancer) && pc.HasDesyncRole())
                {

                    Utils.NotifyRoles(SpecifySeer: pc, ForceLoop: false, MushroomMixupIsActive: true, SendOption: SendOption.None);
                }
            }

            ReportDeadBodyPatch.CanReport.SetAllValues(false);
        }
    }
    [HarmonyPatch(typeof(MushroomMixupSabotageSystem), nameof(MushroomMixupSabotageSystem.Deteriorate))]
    private static class MushroomMixupSabotageSystemPatch
    {
        private static void Prefix(MushroomMixupSabotageSystem __instance, ref bool __state)
        {
            __state = __instance.IsActive;

            if (!Options.SabotageTimeControl.GetBool()) return;
            if (!GameStates.FungleIsActive) return;

            if (!__instance.IsActive || !SetDurationMushroomMixupSabotage)
            {
                if (!SetDurationMushroomMixupSabotage && !__instance.IsActive)
                {
                    SetDurationMushroomMixupSabotage = true;
                }
                return;
            }

            Logger.Info($" {ShipStatus.Instance.Type}", "MushroomMixupSabotageSystem - ShipStatus.Instance.Type");
            Logger.Info($" {SetDurationMushroomMixupSabotage}", "MushroomMixupSabotageSystem - SetDurationCriticalSabotage");
            SetDurationMushroomMixupSabotage = false;

            __instance.currentSecondsUntilHeal = Options.FungleMushroomMixupDuration.GetFloat();
        }
        public static void Postfix(MushroomMixupSabotageSystem __instance, bool __state)
        {
            if (GameStates.IsHideNSeek) return;

            if (__instance.IsActive != __state && !Main.MeetingIsStarted)
            {
                Logger.Info($" IsEnd", "MushroomMixupSabotageSystem.Deteriorate.Postfix");

                if (AmongUsClient.Instance.AmHost)
                {
                    _ = new LateTask(() =>
                    {

                        foreach (PlayerControl pc in Main.EnumerateAlivePlayerControls())
                        {

                            pc.DoUnShiftState(true);

                            if (pc.GetCustomRole().GetRoleTypes() != RoleTypes.Engineer)
                            {
                                pc.RpcResetAbilityCooldown();
                            }

                            ReportDeadBodyPatch.CanReport.SetAllValues(true);
                        }
                    }, 1.2f, "Reset Ability Cooldown Arter Mushroom Mixup");

                    foreach (var pc in Main.EnumerateAlivePlayerControls())
                    {
                        if ((!pc.Is(Custom_Team.Impostor) || Main.PlayerStates[pc.PlayerId].IsNecromancer) && pc.HasDesyncRole())
                        {

                            Utils.NotifyRoles(SpecifySeer: pc, ForceLoop: false, SendOption: SendOption.None);
                        }
                    }
                }
            }
        }
    }
    [HarmonyPatch(typeof(SwitchSystem), nameof(SwitchSystem.UpdateSystem))]
    private static class SwitchSystemUpdatePatch
    {
        private static bool Prefix(SwitchSystem __instance, [HarmonyArgument(0)] PlayerControl player, [HarmonyArgument(1)] MessageReader msgReader)
        {
            if (GameStates.IsHideNSeek) return false;

            byte amount;
            {
                var newReader = MessageReader.Get(msgReader);
                amount = newReader.ReadByte();
                newReader.Recycle();
            }

            if (!AmongUsClient.Instance.AmHost)
            {
                return true;
            }

            if (amount.HasBit(SwitchSystem.DamageSystem))
            {
                return true;
            }

            if (GameStates.AirshipIsActive)
            {
                var truePosition = player.GetCustomPosition();
                if (Options.DisableAirshipViewingDeckLightsPanel.GetBool() && Utils.GetDistance(truePosition, new(-12.93f, -11.28f)) <= 2f) return false;
                if (Options.DisableAirshipGapRoomLightsPanel.GetBool() && Utils.GetDistance(truePosition, new(13.92f, 6.43f)) <= 2f) return false;
                if (Options.DisableAirshipCargoLightsPanel.GetBool() && Utils.GetDistance(truePosition, new(30.56f, 2.12f)) <= 2f) return false;
            }

            if (Fool.IsEnable && player.Is(CustomRoles.Fool))
            {
                return false;
            }

            if (Options.BlockDisturbancesToSwitches.GetBool())
            {

                var switchedKnob = (byte)(0b_00001 << amount);

                if ((__instance.ActualSwitches & switchedKnob) == (__instance.ExpectedSwitches & switchedKnob))
                {
                    return false;
                }
            }

            return true;
        }
    }
    [HarmonyPatch(typeof(ElectricTask), nameof(ElectricTask.Initialize))]
    public static class ElectricTaskInitializePatch
    {
        private static long LastUpdate;
        public static void Postfix()
        {
            long now = Utils.TimeStamp;
            if (LastUpdate >= now) return;
            LastUpdate = now;

            Utils.SyncAllSettings();

            if (GameStates.IsInTask)
            {
                foreach (var pc in Main.EnumerateAlivePlayerControls())
                    if (pc.Is(CustomRoles.Mare))
                        Utils.NotifyRoles(SpecifyTarget: pc, SendOption: SendOption.None);
            }

            Logger.Info("Lights sabotage called", "ElectricTask");
        }
    }
    [HarmonyPatch(typeof(ElectricTask), nameof(ElectricTask.Complete))]
    public static class ElectricTaskCompletePatch
    {
        private static long LastUpdate;

        public static void Postfix()
        {
            long now = Utils.TimeStamp;
            if (LastUpdate >= now) return;
            LastUpdate = now;

            Utils.MarkEveryoneDirtySettings();

            if (GameStates.IsInTask)
            {
                foreach (PlayerControl pc in Main.EnumerateAlivePlayerControls())
                    if (pc.Is(CustomRoles.Mare))
                        Utils.NotifyRoles(SpecifyTarget: pc, SendOption: SendOption.None);
            }

            Logger.Info("Lights sabotage fixed", "ElectricTask");
        }
    }

    [HarmonyPatch(typeof(SabotageSystemType), nameof(SabotageSystemType.UpdateSystem))]
    public static class SabotageSystemTypeRepairDamagePatch
    {
        public static bool isCooldownModificationEnabled;
        public static float modifiedCooldownSec;

        public static void Initialize()
        {
            isCooldownModificationEnabled = Options.SabotageCooldownControl.GetBool();
            modifiedCooldownSec = Options.SabotageCooldown.GetFloat();
        }

        private static bool Prefix([HarmonyArgument(0)] PlayerControl player, [HarmonyArgument(1)] MessageReader msgReader)
        {
            if (GameStates.IsHideNSeek) return false;

            byte amount;
            {
                var newReader = MessageReader.Get(msgReader);
                amount = newReader.ReadByte();
                newReader.Recycle();
            }
            var nextSabotage = (SystemTypes)amount;

            if (Options.DisableSabotage.GetBool())
            {
                return false;
            }

            Logger.Info($"PlayerName: {player.GetNameWithRole()}, SabotageType: {nextSabotage}, amount {amount}", "SabotageSystemType.UpdateSystem");

            return CanSabotage(player, nextSabotage);
        }
        private static bool CanSabotage(PlayerControl player, SystemTypes systemType)
        {
            if (systemType is SystemTypes.Comms)
            {
                if (Camouflager.CantPressCommsSabotageButton(player))
                    return false;
            }

            if (player.Is(CustomRoles.Runner))
            {
                return false;
            }

            if (TimeAssassin.TimeStop || TimeMaster.Rewinding)
            {
                return false;
            }

            return player.CanUseSabotage();
        }

        public static void Postfix(SabotageSystemType __instance, bool __runOriginal)
        {

            if (!AmongUsClient.Instance.AmHost || GameStates.IsHideNSeek || !__runOriginal || !isCooldownModificationEnabled)
            {
                return;
            }

            __instance.Timer = modifiedCooldownSec;
            __instance.IsDirty = true;
        }

        [HarmonyPatch(typeof(SecurityCameraSystemType), nameof(SecurityCameraSystemType.UpdateSystem))]
        private static class SecurityCameraSystemTypeUpdateSystemPatch
        {
            private static bool Prefix([HarmonyArgument(1)] MessageReader msgReader)
            {
                byte amount;
                {
                    var newReader = MessageReader.Get(msgReader);
                    amount = newReader.ReadByte();
                    newReader.Recycle();
                }

                if (amount == SecurityCameraSystemType.IncrementOp)
                {
                    var camerasDisabled = Utils.GetActiveMapName() switch
                    {
                        MapNames.Skeld or MapNames.Dleks => Options.DisableSkeldCamera.GetBool(),
                        MapNames.Polus => Options.DisablePolusCamera.GetBool(),
                        MapNames.Airship => Options.DisableAirshipCamera.GetBool(),
                        _ => false,
                    };
                    return !camerasDisabled;
                }
                return true;
            }
        }
    }
    [HarmonyPatch(typeof(DoorsSystemType), nameof(DoorsSystemType.UpdateSystem))]
    public static class DoorsSystemTypePatch
    {
        public static void Prefix( PlayerControl player, MessageReader msgReader)
        {
            byte amount;
            {
                var newReader = MessageReader.Get(msgReader);
                amount = newReader.ReadByte();
                newReader.Recycle();
            }

            Logger.Info($"Door is opened by {player?.Data?.PlayerName}, amount: {amount}", "DoorsSystemType.UpdateSystem");
        }
    }
}
