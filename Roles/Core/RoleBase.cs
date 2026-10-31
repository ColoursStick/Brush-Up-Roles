using AmongUs.GameOptions;
using Hazel;
using System.Text;
using TONE.Roles.Core;
using UnityEngine;

namespace TONE;

public abstract class RoleBase
{
    public abstract CustomRoles Role { get; }
    public PlayerState _state;
#pragma warning disable IDE1006
    public PlayerControl _Player => _state != null ? Utils.GetPlayerById(_state.PlayerId) ?? null : null;
    public List<byte> _playerIdList => Main.PlayerStates.Values.Where(x => x.MainRole == _state.MainRole).Select(x => x.PlayerId).Cast<byte>().ToList();
#pragma warning restore IDE1006

    public virtual bool IsEnable { get; set; } = false;
    public bool HasVoted = false;
    public virtual bool IsExperimental => false;
    public virtual bool IsDesyncRole => false;
    public virtual bool IsBalance => false;

    public void OnInit()
    {
        IsEnable = false;
        Init();
    }

    public void OnAdd(byte playerid)
    {
        _state = Main.PlayerStates.GetValueOrDefault(playerid);

        if (_state == null)
        {
            Logger.Warn($"Player state {playerid} is null", "RoleBase.OnAdd");
        }

        try
        {
            CustomRoleManager.RoleClass.FirstOrDefault(r => r.Key == _state.MainRole).Value.IsEnable = true;
            this.IsEnable = true;
        }
        catch { }

        Add(playerid);
        if (CustomRoleManager.OtherCollectionsSet)
        {
            CustomRoleManager.Add();
        }

        if (IsDesyncRole)
        {
            Main.DesyncPlayerList.Add(playerid);
        }
    }
    public void OnRemove(byte playerId)
    {
        Remove(playerId);
        IsEnable = false;

        Main.UnShapeShifter.Remove(playerId);

        if (IsDesyncRole)
        {
            Main.DesyncPlayerList.Remove(playerId);
        }
    }

    public virtual void Init()
    { }

    public virtual void Add(byte playerId)
    { }

    public virtual void Remove(byte playerId)
    { }

    public abstract CustomRoles ThisRoleBase { get; }

    public abstract Custom_RoleType ThisRoleType { get; }

    public CustomRoles ThisCustomRole => Role;

    public virtual void SetDesyncImpostorBuddies(ref Dictionary<PlayerControl, List<PlayerControl>> DesyncImpostorBuddy, PlayerControl caller)
    {

    }

    public virtual bool CanUseKillButton(PlayerControl pc) => pc.Is(Custom_Team.Impostor) && pc.IsAlive();

    public virtual bool CanUseImpostorVentButton(PlayerControl pc) => pc.Is(Custom_Team.Impostor) && pc.IsAlive();

    public virtual bool CanUseSabotage(PlayerControl pc) => pc.Is(Custom_Team.Impostor);

    public virtual bool OnSabotage(PlayerControl pc) => pc;

    public virtual bool BlockMoveInVent(PlayerControl pc) => false;

    public HashSet<int> LastBlockedMoveInVentVents = [];

    public virtual void SetupCustomOption()
    { }

    public virtual void ApplyGameOptions(IGameOptions opt, byte playerId)
    {

        opt.SetVision(false);
    }

    public virtual void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = Options.DefaultKillCooldown;

    public virtual void OnFixedUpdate(PlayerControl player, bool lowLoad, long nowTime, int timerLowLoad)
    { }

    public virtual bool OnTaskComplete(PlayerControl player, int completedTaskCount, int totalTaskCount) => true;

    public virtual void OnOthersTaskComplete(PlayerControl pc, PlayerTask task, bool playerIsOverridden, PlayerControl realPlayer)
    { }

    public virtual bool HasTasks(NetworkedPlayerInfo player, CustomRoles role, bool ForRecompute) => role.IsCrewmate() && !role.IsTasklessCrewmate() && (!ForRecompute || !player.Object.IsAnySubRole(x => x.IsConverted()));

    public virtual bool OnCheckProtect(PlayerControl angel, PlayerControl target) => angel && target;

    public virtual bool CheckBootFromVent(PlayerPhysics physics, int ventId) => !physics;

    public virtual bool OnCoEnterVentOthers(PlayerPhysics physics, int ventId) => !physics;

    public virtual void OnCoEnterVent(PlayerPhysics physics, int ventId)
    { }

    public virtual void OnEnterVent(PlayerControl pc, Vent vent)
    { }

    public virtual void OnExitVent(PlayerControl pc, int ventId)
    { }

    public virtual void UpdateSystem(ShipStatus __instance, SystemTypes systemType, byte amount, PlayerControl player)
    { }

    public virtual void SwitchSystemUpdate(SwitchSystem __instance, byte amount, PlayerControl player)
    { }

    public virtual bool ForcedCheckMurderAsKiller(PlayerControl killer, PlayerControl target) => target && killer;

    public virtual bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target) => target && killer;

    public virtual bool CheckMurderOnOthersTarget(PlayerControl killer, PlayerControl target) => !target || !killer;

    public virtual bool OnCheckMurderAsKiller(PlayerControl killer, PlayerControl target) => target && killer;

    public virtual void OnMurderPlayerAsKiller(PlayerControl killer, PlayerControl target, bool inMeeting, bool isSuicide)
    { }

    public virtual void OnMurderPlayerAsTarget(PlayerControl killer, PlayerControl target, bool inMeeting, bool isSuicide)
    { }

    public virtual bool KillFlashCheck(PlayerControl killer, PlayerControl target, PlayerControl seer) => false;

    public virtual bool GlobalKillFlashCheck(PlayerControl killer, PlayerControl target, PlayerControl seer) => false;

    public virtual bool CanDesyncShapeshift => false;

    public virtual bool OnCheckShapeshift(PlayerControl shapeshifter, PlayerControl target, ref bool resetCooldown, ref bool shouldAnimate) => true;

    public virtual void OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool IsAnimate, bool shapeshifting)
    { }
    public virtual void OnMeetingShapeshift(PlayerControl shapeshifter, PlayerControl target)
    { }
    public virtual bool OnCheckVanish(PlayerControl phantom) => true;
    public virtual void OnPet(PlayerControl pc)
    { }

    [Obfuscation(Exclude = true)]
    public virtual void UnShapeShiftButton(PlayerControl shapeshifter) { }

    public virtual bool OnCheckStartMeeting(PlayerControl reporter) => reporter.IsAlive();

    public virtual bool OnCheckReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo deadBody, PlayerControl killer) => reporter.IsAlive();

    public virtual void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    { }

    public virtual bool GuessCheck(bool isUI, PlayerControl guesser, PlayerControl target, CustomRoles role, ref bool guesserSuicide) => !target;

    public virtual bool OnRoleGuess(bool isUI, PlayerControl target, PlayerControl guesser, CustomRoles role, ref bool guesserSuicide) => !target;

    public virtual bool CheckMisGuessed(bool isUI, PlayerControl guesser, PlayerControl target, CustomRoles role, ref bool guesserSuicide) => !target;

    public virtual void CheckExile(NetworkedPlayerInfo exiled, ref bool DecidedWinner, bool isMeetingHud, ref string name)
    { }

    public virtual void CheckExileTarget(NetworkedPlayerInfo exiled, ref bool DecidedWinner, bool isMeetingHud, ref string name)
    { }

    public virtual void OnPlayerExiled(PlayerControl player, NetworkedPlayerInfo exiled)
    { }

    public virtual void OnOthersMeetingHudStart(PlayerControl pc)
    { }

    public virtual void OnMeetingHudStart(PlayerControl pc)
    { }

    public virtual void MeetingHudClear()
    { }

    public virtual string PVANameText(PlayerVoteArea pva, PlayerControl seer, PlayerControl target) => string.Empty;

    public virtual void OnCheckForEndVoting(PlayerState.DeathReason deathReason, params byte[] exileIds)
    { }

    public virtual void NotifyAfterMeeting()
    { }

    public virtual void AfterMeetingTasks()
    { }

    public virtual void OnCoEndGame()
    { }

    [Obfuscation(Exclude = true)]
    public virtual bool CheckVote(PlayerControl voter, PlayerControl target) => voter && target;

    public virtual void OnVote(PlayerControl votePlayer, PlayerControl voteTarget)
    { }

    public virtual void OnVoted(PlayerControl votedPlayer, PlayerControl votedTarget)
    { }

    public virtual bool HideVote(PlayerVoteArea PVA) => false;

    public virtual void AddVisualVotes(PlayerVoteArea votedPlayer, ref List<MeetingHud.VoterState> statesList)
    { }

    public virtual int AddRealVotesNum(PlayerVoteArea PVA) => 0;

    public virtual void SetAbilityButtonText(HudManager hud, byte playerId)
    { }

    public virtual Sprite GetKillButtonSprite(PlayerControl player, bool shapeshifting) => null;

    public virtual Sprite GetAbilityButtonSprite(PlayerControl player, bool shapeshifting) => null;
    public virtual Sprite GetPetButtonSprite(PlayerControl player) => null;
    public virtual Sprite ImpostorVentButtonSprite(PlayerControl player) => null;
    public virtual Sprite ReportButtonSprite { get; }
    public virtual Sprite SabotageButtonSprite { get; }

    public virtual string NotifyPlayerName(PlayerControl seer, PlayerControl target, string TargetPlayerName = "", bool IsForMeeting = false) => string.Empty;

    public virtual string GetMark(PlayerControl seer, PlayerControl seen, bool isForMeeting = false) => string.Empty;
    public virtual string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        var sb = new StringBuilder();
        sb.Append(Utils.GetAbilityTimeDisplay(seer, seen));
        return sb.ToString();
    }
    public virtual string GetSuffix(PlayerControl seer, PlayerControl seen, bool isForMeeting = false) => string.Empty;
    [Obfuscation(Exclude = true)]
    public virtual string GetProgressText(byte playerId, bool comms)
    {
        var sb = new StringBuilder();
        sb.Append(Utils.GetTaskCount(playerId, comms));
        sb.Append(Utils.GetAbilityUseLimitDisplay(playerId, sb.Length <= 0));
        return sb.ToString();
    }

    public virtual string GetMarkOthers(PlayerControl seer, PlayerControl seen, bool isForMeeting = false) => string.Empty;
    public virtual string GetLowerTextOthers(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false) => string.Empty;
    public virtual string GetSuffixOthers(PlayerControl seer, PlayerControl seen, bool isForMeeting = false) => string.Empty;

    public virtual bool KnowRoleTarget(PlayerControl seer, PlayerControl target) => false;
    public virtual string PlayerKnowTargetColor(PlayerControl seer, PlayerControl target) => string.Empty;
    public virtual bool OthersKnowTargetRoleColor(PlayerControl seer, PlayerControl target) => false;

    public virtual int CalculatePlayers(int max, int min)
    {
        if (!Options.BalanceRoleSetting.GetBool() || Options.BalanceNeedPlayers.GetInt() < Main.AllAlivePlayerControls.Count) return max;

        return min;
    }

    public virtual bool RoleCommand(PlayerControl pc, string msg, bool isUI = false) => false;

    public virtual bool CreateAbilityButton(PlayerControl pc) => false;

    public virtual bool UseGuessPage => false;

    public virtual bool ShowAbilityButtonFor(PlayerControl target) => false;

    public virtual string AbilityButtonName => string.Empty;

    public virtual void OnClickAbilityButton(byte targetId, CustomRoles role)
    { }

    public virtual void ReceiveRPC(MessageReader reader, PlayerControl pc)
    { }

    [Obfuscation(Exclude = true)]
    public enum GeneralOption
    {

        Cooldown,
        AbilityCooldown,
        AbilityDuration,
        SkillLimitTimes,

        CanKill,
        KillCooldown,
        CanVent,
        CantMoveOnVents,
        ImpostorVision,
        CanUseSabotage,
        CanHaveAccessToVitals,

        CanKillImpostors,
        CanGuess,
        HideVote,
        HideAdditionalVotes,
        CanUseMeetingButton,
        ModeSwitchAction,
        ShowShapeshiftAnimations,

        DefaultKillCooldown,
        ReduceKillCooldown,
        MaxKillCooldown,
        MinKillCooldown,
        KillAttackerWhenAbilityRemaining,
        SnatchesWin,

        ShapeshifterBase_ShapeshiftCooldown,
        ShapeshifterBase_ShapeshiftDuration,
        ShapeshifterBase_LeaveShapeshiftingEvidence,
        PhantomBase_InvisCooldown,
        PhantomBase_InvisDuration,
        GuardianAngelBase_ProtectCooldown,
        GuardianAngelBase_ProtectionDuration,
        GuardianAngelBase_ImpostorsCanSeeProtect,
        ScientistBase_BatteryCooldown,
        ScientistBase_BatteryDuration,
        EngineerBase_VentCooldown,
        EngineerBase_InVentMaxTime,
        NoisemakerBase_ImpostorAlert,
        NoisemakerBase_AlertDuration,
        TrackerBase_TrackingCooldown,
        TrackerBase_TrackingDuration,
        TrackerBase_TrackingDelay,
        DetectiveBase_DetectiveSuspectLimit,
        ViperBase_ViperDissolveTime,
        JudgeBase_JudgeTaskRequirementPercentage,
        InfluencerBase_SpiritGuideCooldownSeconds,
    }
}
