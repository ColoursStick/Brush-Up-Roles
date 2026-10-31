using AmongUs.GameOptions;
using TONE.Modules;
using TONE.Roles.Core;
using TONE.Roles.Neutral;
using UnityEngine;
using static TONE.Options;
using static TONE.Translator;

namespace TONE.Roles.Impostor;

internal class DollMaster : RoleBase
{

    public override CustomRoles Role => CustomRoles.DollMaster;
    private const int Id = 28500;
    public static bool HasEnabled => CustomRoleManager.HasEnabled(CustomRoles.DollMaster);
    public override bool IsExperimental => true;
    public override CustomRoles ThisRoleBase => CustomRoles.Shapeshifter;
    public override Custom_RoleType ThisRoleType => Custom_RoleType.ImpostorKilling;

    private static readonly HashSet<byte> ReducedVisionPlayers = [];
    public static bool IsControllingPlayer = false;
    private static bool ResetPlayerSpeed = false;
    private static bool WaitToUnPossess = false;
    public static PlayerControl controllingTarget = null;
    public static PlayerControl DollMasterTarget = null;
    public static NetworkedPlayerInfo.PlayerOutfit controllingOutfit = null;
    public static NetworkedPlayerInfo.PlayerOutfit DollMasterOutfit = null;
    private static float originalSpeed = float.MinValue;
    private static Vector2 controllingTargetPos = new(0, 0);
    private static Vector2 DollMasterPos = new(0, 0);

    private static OptionItem DefaultKillCooldown;
    private static OptionItem ShapeshiftCooldown;
    private static OptionItem ShapeshiftDuration;
    public static OptionItem CanKillAsMainBody;
    public static OptionItem TargetDiesAfterPossession;

    public override void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.DollMaster);
        DefaultKillCooldown = FloatOptionItem.Create(Id + 10, GeneralOption.KillCooldown, new(0f, 180f, 2.5f), 25f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.DollMaster])
            .SetValueFormat(OptionFormat.Seconds);
        ShapeshiftCooldown = FloatOptionItem.Create(Id + 11, "DollMasterPossessionCooldown", new(0f, 180f, 2.5f), 25f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.DollMaster])
            .SetValueFormat(OptionFormat.Seconds);
        ShapeshiftDuration = FloatOptionItem.Create(Id + 12, "DollMasterPossessionDuration", new(0f, 180f, 2.5f), 10f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.DollMaster])
            .SetValueFormat(OptionFormat.Seconds);
        CanKillAsMainBody = BooleanOptionItem.Create(Id + 13, "DollMasterCanKillAsMainBody", true, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.DollMaster]);
        TargetDiesAfterPossession = BooleanOptionItem.Create(Id + 14, "DollMasterTargetDiesAfterPossession", false, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.DollMaster]);
    }

    public override void Init()
    {
        ReducedVisionPlayers.Clear();
        DollMasterTarget = null;
        controllingTarget = null;
        controllingOutfit = null;
        DollMasterOutfit = null;
    }

    public override void Add(byte playerId)
    {
        DollMasterTarget = Utils.GetPlayerById(playerId);
        IsControllingPlayer = false;
        CustomRoleManager.OnFixedUpdateOthers.Add(OnFixedUpdateOthers);
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.ShapeshifterCooldown = ShapeshiftCooldown.GetFloat();
        AURoleOptions.ShapeshifterDuration = ShapeshiftDuration.GetFloat() + 0.35f;
    }

    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = DefaultKillCooldown.GetFloat();

    public static bool IsDoll(byte id) => ReducedVisionPlayers.Contains(id);

    public static void ApplySettingsToDoll(IGameOptions opt)
    {
        opt.SetVision(false);
        opt.SetFloat(FloatOptionNames.CrewLightMod, 0f * 0);
        opt.SetFloat(FloatOptionNames.ImpostorLightMod, 0f * 0);
    }

    public override void OnFixedUpdate(PlayerControl player, bool lowLoad, long nowTime, int timerLowLoad)
    {
        if (lowLoad) return;
        if (controllingTarget != null && DollMasterTarget != null)
        {

            WaitToUnPossessUpdate(DollMasterTarget, controllingTarget);
        }
        if (IsControllingPlayer && (controllingTarget == null || DollMasterTarget == null))
        {
            ReducedVisionPlayers.Clear();

            if (controllingTarget != null)
            {
                Main.AllPlayerSpeed[controllingTarget.PlayerId] = originalSpeed;
                controllingTarget.MarkDirtySettings();
            }

            DollMasterTarget?.RpcShapeshift(DollMasterTarget, false);
            controllingTarget?.ResetPlayerOutfit();

            IsControllingPlayer = false;
            ResetPlayerSpeed = true;
        }
    }
    private static void OnFixedUpdateOthers(PlayerControl target, bool lowLoad, long nowTime)
    {
        if (lowLoad) return;
        if (controllingTarget != null && target == controllingTarget)
        {

            BootPossessedPlayerFromVentUpdate(target);

            if (IsControllingPlayer && Main.AllPlayerSpeed[target.PlayerId] >= Main.MinSpeed)
            {
                Main.AllPlayerSpeed[target.PlayerId] = Main.MinSpeed;
                target.MarkDirtySettings();
            }
            else if (ResetPlayerSpeed)
            {
                Main.AllPlayerSpeed[target.PlayerId] = originalSpeed;
                target.MarkDirtySettings();
            }
        }
    }

    private static void BootPossessedPlayerFromVentUpdate(PlayerControl target)
    {
        if (IsControllingPlayer && target.inVent && !target.walkingToVent)
        {
            _ = new LateTask(() =>
            {
                if (!target.inVent || target.walkingToVent) return;
                target.MyPhysics.RpcBootFromVent(target.GetPlayerVentId());
            }, 0.25f, "Boot Possessed Player from vent: " + target.GetPlayerVentId());
        }
    }

    private static void WaitToUnPossessUpdate(PlayerControl pc, PlayerControl target)
    {
        if (IsControllingPlayer && WaitToUnPossess)
        {

            if (pc.inVent && !pc.walkingToVent && !pc.MyPhysics.Animations.IsPlayingEnterVentAnimation())
            {
                _ = new LateTask(() =>
                {
                    if (!pc.inVent || pc.walkingToVent || pc.MyPhysics.Animations.IsPlayingEnterVentAnimation()) return;
                    pc.MyPhysics.RpcBootFromVent(pc.GetPlayerVentId());
                }, 0.3f, "Boot DollMaster from vent: " + pc.GetPlayerVentId());
            }

            if (pc.CanBeTeleported() && target.CanBeTeleported())
            {
                _ = new LateTask(() =>
                {
                    if (!WaitToUnPossess) return;
                    UnPossess(pc, target);
                    GetPlayersPositions(pc);
                    SwapPlayersPositions(pc);
                }, 0.15f, "UnPossess");
            }
        }
    }

    public override void OnReportDeadBody(PlayerControl pc, NetworkedPlayerInfo target)
    {
        if (IsControllingPlayer && controllingTarget != null && DollMasterTarget != null)
        {
            UnPossess(DollMasterTarget, controllingTarget);
            Main.AllPlayerSpeed[controllingTarget.PlayerId] = originalSpeed;
            ReducedVisionPlayers.Clear();
        }
    }

    public override bool OnCheckReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo deadBody, PlayerControl killer)
    {
        if (controllingTarget == null || DollMasterTarget == null) return true;

        if (IsControllingPlayer && IsDoll(reporter.PlayerId)) return false;

        if (IsControllingPlayer && reporter.Is(CustomRoles.DollMaster))
        {
            UnPossess(DollMasterTarget, controllingTarget);
            GetPlayersPositions(DollMasterTarget);
            SwapPlayersPositions(DollMasterTarget);
            controllingTarget.CmdReportDeadBody(deadBody);
            return false;
        }

        return true;
    }

    public override bool OnCheckStartMeeting(PlayerControl reporter)
    {
        if (controllingTarget == null || DollMasterTarget == null) return true;

        if (IsControllingPlayer && IsDoll(reporter.PlayerId)) return false;

        if (IsControllingPlayer && reporter.Is(CustomRoles.DollMaster))
        {
            UnPossess(DollMasterTarget, controllingTarget);
            GetPlayersPositions(DollMasterTarget);
            SwapPlayersPositions(DollMasterTarget);
            controllingTarget.CmdReportDeadBody(null);
            return false;
        }

        return true;
    }

    public override bool OnCheckMurderAsKiller(PlayerControl killer, PlayerControl target) => (!(IsControllingPlayer && target == DollMasterTarget));

    public override bool CheckMurderOnOthersTarget(PlayerControl killer, PlayerControl target)
    {
        if (IsControllingPlayer && DollMasterTarget != null && controllingTarget != null)
        {
            if (!CanKillerUseAbility(killer)) return true;

            if (killer.GetCustomRole().IsImpostorTeam() && target == controllingTarget) return true;

            if (killer.Is(CustomRoles.Sheriff) && killer != DollMasterTarget && target == DollMasterTarget)
            {
                CheckMurderAsPossessed(killer, target);
                return true;
            }
        }
        return false;
    }

    private static bool CanKillerUseAbility(PlayerControl player)
    {
        var CanUseAbility = true;
        var cRole = player.GetCustomRole();

        switch (cRole)
        {
            case CustomRoles.Pelican:
                CanUseAbility = false;
                break;
            case CustomRoles.Penguin:
                CanUseAbility = false;
                break;
            default:
                break;
        }

        if (!CanUseAbility) player.Notify(Utils.ColorString(player.GetRoleColor(), GetString("DollMaster_UnableToUseAbility")));

        return CanUseAbility;
    }

    public static void CheckMurderAsPossessed(PlayerControl killer, PlayerControl target)
    {
        if (!IsControllingPlayer || controllingTarget == null || DollMasterTarget == null) return;

        if (target == controllingTarget)
        {
            target.RpcSetPet("");
            UnPossess(DollMasterTarget, controllingTarget);
            GetPlayersPositions(DollMasterTarget);
            SwapPlayersPositions(DollMasterTarget);
            killer.RpcMurderPlayer(controllingTarget);
            return;
        }

        else if (target == DollMasterTarget)
        {
            DollMasterTarget.RpcSetPet("");
            UnPossess(DollMasterTarget, controllingTarget);
            GetPlayersPositions(DollMasterTarget);
            SwapPlayersPositions(DollMasterTarget);
            killer.RpcMurderPlayer(DollMasterTarget);
            return;
        }
    }

    public override bool CanUseKillButton(PlayerControl pc) => CanKillAsMainBody.GetBool() || IsControllingPlayer;
    public override bool CanUseImpostorVentButton(PlayerControl pc) => !IsControllingPlayer;

    public override bool OnCheckShapeshift(PlayerControl pc, PlayerControl target, ref bool resetCooldown, ref bool shouldAnimate)
    {
        if (WaitToUnPossess) return false;
        if (target == null) return false;
        shouldAnimate = false;
        resetCooldown = false;

        if (!pc.CanBeTeleported() && IsControllingPlayer)
        {
            WaitToUnPossess = true;
            return false;
        }

        if (controllingTarget != null && !controllingTarget.CanBeTeleported() && IsControllingPlayer)
        {
            WaitToUnPossess = true;
            return false;
        }

        if (!IsControllingPlayer && target.GetCustomRole().IsImpostorTeam() && !pc.Is(CustomRoles.Narc))
        {
            AURoleOptions.ShapeshifterCooldown = 0;
            pc.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.DollMaster), GetString("DollMaster_CannotPossessImpTeammate")));
            return false;
        }

        if (!IsControllingPlayer
            && pc.Is(CustomRoles.Narc) && target.IsPolice())
        {
            AURoleOptions.ShapeshifterCooldown = 0;
            pc.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Sheriff), GetString("DollMaster_CannotPossessImpTeammate")));
            return false;
        }

        if (!target.CanBeTeleported() || Pelican.IsEaten(pc.PlayerId) || Pelican.IsEaten(target.PlayerId))
        {
            AURoleOptions.ShapeshifterCooldown = 0;
            pc.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.DollMaster), target.IsAlive() ? GetString("DollMaster_CouldNotSwapWithTarget") : GetString("DollMaster_CanNotSwapWithDeadTarget")));
            return false;
        }

        ReducedVisionPlayers.Add(target.PlayerId);

        if (!IsControllingPlayer)
        {
            if (target != null) controllingTarget = target;
            originalSpeed = Main.AllPlayerSpeed[target.PlayerId];
            IsControllingPlayer = true;

            _ = new LateTask(() =>
            {
                if (GameStates.IsMeeting || GameStates.IsExilling)
                {
                    IsControllingPlayer = false;
                    return;
                }

                Possess(pc, target);
                GetPlayersPositions(pc);
                SwapPlayersPositions(pc);
            }, 0.35f);
            return false;
        }
        else if (controllingTarget != null)
        {
            UnPossess(pc, controllingTarget);
            GetPlayersPositions(pc);
            SwapPlayersPositions(pc);
        }
        return false;
    }

    private static void Possess(PlayerControl pc, PlayerControl target)
    {
        DollMasterOutfit = new NetworkedPlayerInfo.PlayerOutfit()
            .Set(pc.GetRealName(), pc.CurrentOutfit.ColorId, pc.CurrentOutfit.HatId, pc.CurrentOutfit.SkinId, pc.CurrentOutfit.VisorId, pc.CurrentOutfit.PetId, pc.CurrentOutfit.NamePlateId);
        controllingOutfit = new NetworkedPlayerInfo.PlayerOutfit()
            .Set(target.GetRealName(), target.CurrentOutfit.ColorId, target.CurrentOutfit.HatId, target.CurrentOutfit.SkinId, target.CurrentOutfit.VisorId, target.CurrentOutfit.PetId, target.CurrentOutfit.NamePlateId);

        (target.MyPhysics.FlipX, pc.MyPhysics.FlipX) = (pc.MyPhysics.FlipX, target.MyPhysics.FlipX);
        pc?.RpcShapeshift(target, false);

        pc?.SetNewOutfit(controllingOutfit);
        target?.SetNewOutfit(DollMasterOutfit);

        pc?.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.DollMaster), GetString("DollMaster_PossessedTarget")));
    }

    private static void UnPossess(PlayerControl pc, PlayerControl target)
    {
        (target.MyPhysics.FlipX, pc.MyPhysics.FlipX) = (pc.MyPhysics.FlipX, target.MyPhysics.FlipX);

        pc?.RpcShapeshift(pc, false);
        pc?.SetNewOutfit(DollMasterOutfit);
        target?.SetNewOutfit(controllingOutfit);

        IsControllingPlayer = false;
        ResetPlayerSpeed = true;
        _ = new LateTask(() =>
        {
            ReducedVisionPlayers.Clear();
            if (TargetDiesAfterPossession.GetBool() && !GameStates.IsMeeting) target?.RpcMurderPlayer(target);
        }, 0.45f);
    }

    public static PlayerControl SwapPlayerInfo(PlayerControl player)
    {
        if (IsControllingPlayer && HasEnabled)
        {
            if (DollMasterTarget != null && controllingTarget != null)
            {
                if (player == DollMasterTarget)
                    return controllingTarget;
                else if (player == controllingTarget)
                    return DollMasterTarget;
            }
        }

        return player;
    }

    private static void GetPlayersPositions(PlayerControl pc)
    {
        if (controllingTarget == null) return;
        controllingTargetPos = controllingTarget.GetCustomPosition();
        DollMasterPos = pc.GetCustomPosition();
    }

    private static void SwapPlayersPositions(PlayerControl pc)
    {
        if (controllingTarget == null) return;
        controllingTarget?.RpcTeleport(DollMasterPos);
        pc?.RpcTeleport(controllingTargetPos);
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target = null, bool isForMeeting = false)
    {
        if (!IsControllingPlayer || GameStates.IsMeeting || isForMeeting) return string.Empty;
        if (controllingTarget == null || DollMasterTarget == null || target == null) return string.Empty;

        if (seer.PlayerId != target.PlayerId && target.PlayerId == controllingTarget.PlayerId)
            return "<color=#ffea00>" + GetString("DollMaster_MainBody") + "</color>";

        if (seer.PlayerId == target.PlayerId && target.PlayerId == DollMasterTarget.PlayerId)
            return "<color=#ffea00>" + GetString("DollMaster_Doll") + "</color>";

        return string.Empty;
    }

    public override void SetAbilityButtonText(HudManager hud, byte playerId) => hud.AbilityButton.OverrideText(GetString(IsControllingPlayer ? "DollMasterUnPossessionButtonText" : "DollMasterPossessionButtonText"));

    public override Sprite GetAbilityButtonSprite(PlayerControl player, bool shapeshifting) => CustomButton.Get("Puttpuer");
}
