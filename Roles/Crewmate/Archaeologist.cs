using AmongUs.GameOptions;
using Hazel;
using System.Text;
using TONE.Modules;
using TONE.Modules.Rpc;
using TONE.Roles.AddOns;
using TONE.Roles.AddOns.Common;
using TONE.Roles.Neutral;
using static TONE.Options;
using static TONE.SabotageSystemPatch;
using static TONE.Translator;

namespace TONE.Roles.Crewmate;

internal class Archaeologist : RoleBase
{

    public override CustomRoles Role => CustomRoles.Archaeologist;
    private const int Id = 32100;
    public override CustomRoles ThisRoleBase => UsePets.GetBool() ? CustomRoles.Crewmate : CustomRoles.Engineer;
    public override bool IsExperimental => true;
    public override Custom_RoleType ThisRoleType => Custom_RoleType.CrewmateBasic;
    public override bool BlockMoveInVent(PlayerControl pc) => true;

    private static List<CustomRoles> addons = [];
    private static readonly HashSet<byte> SlateRoles = [];
    private static byte SlatePlayerId = byte.MaxValue;
    private static SystemTypes CurrentActiveSabotage = SystemTypes.Hallway;

    public static OptionItem VentCooldown;
    private static OptionItem InvisDuration;
    private static OptionItem FreezeTime;
    private static OptionItem FreezeRadius;
    private static OptionItem BugleSpeed;
    private static OptionItem BugleTime;
    private static OptionItem CrystalExtraVotes;
    private static OptionItem TalismanDuration;
    private static OptionItem CurseKillCooldown;

    private static byte AntiqueID = 251;
    private static bool FixNextSabo = false;
    private static bool IsProtected = false;
    private static bool HasGrail = false;
    private static bool LifeConnection = false;
    private static float Votes = 0;

    public override void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Archaeologist, 1, zeroOne: false);
        VentCooldown = FloatOptionItem.Create(Id + 11, GeneralOption.EngineerBase_VentCooldown, new(0f, 70f, 1f), 15f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Archaeologist])
            .SetValueFormat(OptionFormat.Seconds);
        InvisDuration = FloatOptionItem.Create(Id + 12, "SwooperDuration", new(5f, 70f, 1f), 20f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Archaeologist])
            .SetValueFormat(OptionFormat.Seconds);
        FreezeTime = FloatOptionItem.Create(Id + 13, "FreezeTime", new(0f, 10f, 1f), 5f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Archaeologist])
            .SetValueFormat(OptionFormat.Seconds);
        FreezeRadius = FloatOptionItem.Create(Id + 14, "FreezeRadius", new(0.5f, 10f, 0.5f), 2f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Archaeologist])
            .SetValueFormat(OptionFormat.Multiplier);
        BugleSpeed = FloatOptionItem.Create(Id + 15, "BugleSpeed", new(0f, 5f, 0.25f), 2.5f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Archaeologist])
            .SetValueFormat(OptionFormat.Multiplier);
        BugleTime = FloatOptionItem.Create(Id + 16, "BugleTime", new(0f, 10f, 1f), 10f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Archaeologist])
            .SetValueFormat(OptionFormat.Seconds);
        CrystalExtraVotes = FloatOptionItem.Create(Id + 17, "CrystalExtraVotes", new(1f, 5f, 1f), 1f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Archaeologist])
            .SetValueFormat(OptionFormat.Votes);
        TalismanDuration = FloatOptionItem.Create(Id + 18, "TalismanDuration", new(5f, 60f, 1f), 15f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Archaeologist])
            .SetValueFormat(OptionFormat.Seconds);
        CurseKillCooldown = FloatOptionItem.Create(Id + 19, "CurseKillCooldown", new(0f, 100f, 2.5f), 40f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Archaeologist])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Init()
    {
        IsProtected = false;
        AntiqueID = 251;
        FixNextSabo = false;
        HasGrail = false;
        Votes = 0;
        LifeConnection = false;
        addons.Clear();
        addons.AddRange(GroupedAddons[AddonTypes.Helpful]);
        SlatePlayerId = byte.MaxValue;
    }
    public override void Add(byte playerId)
    {
        if (AmongUsClient.Instance.AmHost)
        {
            _ = new LateTask(() =>
            {
                if (GameStates.IsInTask)
                {
                    RandomAntique();
                }
            }, 8f, "Archaeologist In Start");
        }
    }
    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.EngineerCooldown = VentCooldown.GetFloat();
        AURoleOptions.EngineerInVentMaxTime = 1;
    }

    public override void AfterMeetingTasks()
    {
        RandomAntique();
    }

    private static void SendRPC(PlayerControl pc)
    {
        if (!pc.IsNonHostModdedClient()) return;
        var msg = new RpcSetArchaeologist(PlayerControl.LocalPlayer.NetId, FixNextSabo, AntiqueID, SlatePlayerId);
        RpcUtils.LateBroadcastReliableMessage(msg);
    }
    public static void ReceiveRPC(MessageReader reader)
    {
        FixNextSabo = reader.ReadBoolean();
        AntiqueID = reader.ReadByte();
        SlatePlayerId = reader.ReadByte();
        SlateRoles.Add(SlatePlayerId);
    }

    public override void OnPet(PlayerControl pc)
    {
        OnEnterVent(pc, null);
    }
    public override void OnEnterVent(PlayerControl player, Vent vent)
    {
        if (FixNextSabo)
        {
            FixAllSabotage();
            return;
        }
        switch (AntiqueID)
        {
            case 1:
                player.RpcGuardAndKill();
                player.RpcSetCustomRole(CustomRoles.Seer, false, false);
                break;
            case 2:
                player.RpcGuardAndKill();
                player.Notify(GetString("GrailNeedReport"), 5f);
                HasGrail = true;
                break;
            case 3:
                player.RpcGuardAndKill();
                player.RpcMakeInvisible();
                player.Notify(GetString("SwooperInvisState"), InvisDuration.GetFloat() - 1);
                _ = new LateTask(() =>
                {
                    if (!GameStates.IsInTask || ExileController.Instance || AntiBlackout.SkipTasks) return;
                    player.RpcMakeVisible();
                    player.RpcGuardAndKill();
                    player.Notify(GetString("SwooperInvisStateOut"), 5f);
                }, InvisDuration.GetFloat());
                break;
            case 4:
                player.RpcGuardAndKill();
                foreach (var target in Main.EnumerateAlivePlayerControls())
                {
                    target.RpcTeleport(player.GetCustomPosition());
                    target.RPCPlayCustomSound("Teleport");
                }
                break;
            case 5:
                player.RpcGuardAndKill();
                foreach (var target in Main.EnumerateAlivePlayerControls())
                {
                    var pos = player.transform.position;
                    var dis = Utils.GetDistance(pos, target.transform.position);
                    if (dis > FreezeRadius.GetFloat()) continue;

                    var tmpSpeed = Main.AllPlayerSpeed[target.PlayerId];
                    Main.AllPlayerSpeed[target.PlayerId] = Main.MinSpeed;
                    target.Notify(GetString("OrbsAndFreeze"), 5f);
                    target.MarkDirtySettings();
                    _ = new LateTask(() =>
                    {
                        Main.AllPlayerSpeed[target.PlayerId] = Main.AllPlayerSpeed[target.PlayerId] - Main.MinSpeed + tmpSpeed;
                        target.MarkDirtySettings();
                        RPC.PlaySoundRPC(Sounds.TaskComplete, target.PlayerId);
                    }, FreezeTime.GetFloat());
                }
                break;
            case 6:
                player.RpcGuardAndKill();
                foreach (var target in Main.EnumerateAlivePlayerControls())
                {
                    var tmpSpeed = Main.AllPlayerSpeed[target.PlayerId];
                    Main.AllPlayerSpeed[target.PlayerId] = BugleSpeed.GetFloat();
                    target.Notify(GetString("BugleAndSpeedIncrease"), 5f);
                    target.MarkDirtySettings();
                    _ = new LateTask(() =>
                    {
                        Main.AllPlayerSpeed[target.PlayerId] = Main.AllPlayerSpeed[target.PlayerId] - BugleSpeed.GetFloat() + tmpSpeed;
                        target.MarkDirtySettings();
                        RPC.PlaySoundRPC(Sounds.TaskComplete, target.PlayerId);
                    }, BugleTime.GetFloat());
                }
                break;
            case 7:
                player.RpcGuardAndKill();
                var randomPlayer = Main.EnumerateAlivePlayerControls().Where(pc => player.PlayerId != pc.PlayerId && pc.Is(Custom_Team.Crewmate) && Utils.HasTasks(pc.Data, false)).ToList().RandomElement();

                if (randomPlayer == null)
                {
                    player.Notify(GetString("TaskManager_FailCompleteRandomTasks"));
                }

                var allNotCompletedTasks = randomPlayer.Data.Tasks.ToArray().Where(pcTask => !pcTask.Complete).ToList();

                if (allNotCompletedTasks.Count > 0)
                {
                    randomPlayer.RpcCompleteTask(allNotCompletedTasks.RandomElement().Id);

                    player.Notify(GetString("TaskManager_YouCompletedRandomTask"));
                    randomPlayer.Notify(GetString("TaskManager_CompletedRandomTaskForPlayer"));
                }
                break;
            case 8:
                player.RpcGuardAndKill();
                foreach (var target in Main.EnumeratePlayerControls().Where(x => x.IsPlayerCrewmateTeam()))
                    target.RpcIncreaseAbilityUseLimitBy(1);
                break;
            case 9:
                player.RpcGuardAndKill();
                player.Notify(GetString("VotesIncreased"));
                Votes += CrystalExtraVotes.GetFloat();
                break;
            case 10:
                player.RpcGuardAndKill();
                player.Notify(GetString("LifeConnection"));
                LifeConnection = true;
                break;
            case 11:
                player.RpcGuardAndKill();
                player.RpcMurderPlayer(player);
                player.SetDeathReason(PlayerState.DeathReason.Sacrifice);
                foreach (var target in Main.EnumerateAlivePlayerControls().Where(x => x.IsPlayerImpostorTeam() || x.IsPlayerNeutralTeam() || x.IsPlayerCovenTeam()))
                {
                    target.Notify(GetString("AnAncienCurseAffectsYou"));
                    target.SetKillCooldown(CurseKillCooldown.GetFloat(), forceAnime: true);
                }
                foreach (var target in Main.EnumerateAlivePlayerControls().Where(x => x.IsPlayerCrewmateTeam()))
                {
                    CustomRoles addon = addons.RandomElement();
                    target.Notify(GetString("AnAncienBlessingsAffectsYou"));
                    target.RpcSetCustomRole(addon);
                }
                break;
            case 12:
                player.RpcGuardAndKill();
                IsProtected = true;
                player.RPCPlayCustomSound("Shield");
                player.Notify(GetString("ArShielded"), TalismanDuration.GetInt());

                _ = new LateTask(() =>
                {
                    IsProtected = false;
                    player.Notify(GetString("ArShieldedOut"));

                }, TalismanDuration.GetInt(), "Archaeologist Shield Is Out");
                break;
            case 13:
                player.RpcGuardAndKill();
                var pcList = Main.EnumerateAlivePlayerControls().Where(pc => pc.PlayerId != player.PlayerId && !pc.GetCustomRole().IsRevealingRole(pc)).ToList();

                PlayerControl rp = pcList.RandomElement();
                if (pcList.Any() && !SlateRoles.Contains(rp.PlayerId))
                {
                    SlateRoles.Add(rp.PlayerId);
                    SlatePlayerId = rp.PlayerId;
                    SendRPC(player);
                }
                break;
            case 14:
                player.RpcGuardAndKill();
                foreach (var target in Main.EnumerateAlivePlayerControls()) target.SetKillCooldown();
                break;
            case 15:
                player.RpcGuardAndKill();
                FixNextSabo = true;
                FixAllSabotage();
                break;
            case 16:
                if (!UsePets.GetBool()) player?.MyPhysics?.RpcBootFromVent(vent.Id);
                player?.NoCheckStartMeeting(null);
                break;
            case 17:
                player.Notify(GetString("ArOpenAllDoors"));
                DoorsReset.OpenAllDoors();
                break;
            case 18:
                player.Notify(GetString("ArResetSabotageCooldown"));
                var sabotageSystemType = ShipStatus.Instance.Systems[SystemTypes.Sabotage].CastFast<SabotageSystemType>();
                sabotageSystemType.Timer = SabotageSystemTypeRepairDamagePatch.isCooldownModificationEnabled
                    ? SabotageSystemTypeRepairDamagePatch.modifiedCooldownSec
                    : 30f;
                sabotageSystemType.IsDirty = true;
                break;
            default:
                break;
        }

        AntiqueID = 251;
        SendRPC(player);
    }

    public override int AddRealVotesNum(PlayerVoteArea PVA) => (int)Votes;
    public override void AddVisualVotes(PlayerVoteArea votedPlayer, ref List<MeetingHud.VoterState> statesList)
    {
        for (var i = 0; i < Votes; i++)
        {
            statesList.Add(new MeetingHud.VoterState()
            {
                VoterId = votedPlayer.PlayerId,
                VotedForId = votedPlayer.VotedForId
            });
        }
    }

    public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
    {
        if (LifeConnection)
        {
            var pcList = Main.EnumerateAlivePlayerControls().Where(pc => pc.PlayerId != target.PlayerId && !Pelican.IsEaten(pc.PlayerId) && !Guardian.CannotBeKilled(pc) && !Medic.IsProtected(pc.PlayerId)
            && !pc.Is(CustomRoles.Pestilence) && !pc.Is(CustomRoles.Necromancer) && !pc.Is(CustomRoles.PunchingBag) && !pc.Is(CustomRoles.Solsticer) && !(pc.Is(CustomRoles.Mini) && Mini.Age < 18)).ToList();

            if (pcList.Any())
            {
                PlayerControl rp = pcList.RandomElement();
                rp.SetDeathReason(PlayerState.DeathReason.Targeted);
                rp.RpcMurderPlayer(rp);
                rp.SetRealKiller(target);
            }
            LifeConnection = false;
            return true;
        }
        if (!IsProtected) return true;

        killer.SetKillCooldown(time: 5f);
        return false;
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        if (seer == null || !seer.IsAlive() || isForMeeting || !isForHud) return string.Empty;

        var str = new StringBuilder();
        switch (AntiqueID)
        {
            case 1:
                str.Append(GetString("PotionStore") + GetString("Ara"));
                break;
            case 2:
                str.Append(GetString("PotionStore") + GetString("Arb"));
                break;
            case 3:
                str.Append(GetString("PotionStore") + GetString("Arc"));
                break;
            case 4:
                str.Append(GetString("PotionStore") + GetString("Ard"));
                break;
            case 5:
                str.Append(GetString("PotionStore") + GetString("Are"));
                break;
            case 6:
                str.Append(GetString("PotionStore") + GetString("Arf"));
                break;
            case 7:
                str.Append(GetString("PotionStore") + GetString("Arg"));
                break;
            case 8:
                str.Append(GetString("PotionStore") + GetString("Arh"));
                break;
            case 9:
                str.Append(GetString("PotionStore") + GetString("Ari"));
                break;
            case 10:
                str.Append(GetString("PotionStore") + GetString("Arj"));
                break;
            case 11:
                str.Append(GetString("PotionStore") + GetString("Ark"));
                break;
            case 12:
                str.Append(GetString("PotionStore") + GetString("Arl"));
                break;
            case 13:
                str.Append(GetString("PotionStore") + GetString("Arm"));
                break;
            case 14:
                str.Append(GetString("PotionStore") + GetString("Arn"));
                break;
            case 15:
                str.Append(GetString("PotionStore") + GetString("Aro"));
                break;
            case 16:
                str.Append(GetString("PotionStore") + GetString("Arp"));
                break;
            case 17:
                str.Append(GetString("PotionStore") + GetString("Arq"));
                break;
            case 18:
                str.Append(GetString("PotionStore") + GetString("Arr"));
                break;
            default:
                break;
        }
        if (FixNextSabo) str.Append(GetString("PotionStore") + GetString("Aro"));
        if (UsePets.GetBool()) str.Append("\n" + Utils.GetAbilityTimeDisplay(seer, seen));
        return str.ToString();
    }
    public override void SetAbilityButtonText(HudManager hud, byte playerId)
    {
        if (!UsePets.GetBool())
        {
            hud.AbilityButton.OverrideText(GetString("ArchaeologistVentButtonText"));
        }
        else
        {
            hud.PetButton.OverrideText(GetString("ArchaeologistVentButtonText"));
        }
    }
    public override bool OnCheckReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo deadBody, PlayerControl killer)
    {
        if (deadBody == null || deadBody.Object == null) return true;
        if (Main.UnreportableBodies.Contains(deadBody.PlayerId)) return false;
        if (reporter.Is(CustomRoles.Archaeologist) && _Player?.PlayerId == reporter.PlayerId && HasGrail)
        {
            var deadPlayer = deadBody.Object;
            var deadPlayerId = deadPlayer.PlayerId;
            var deadBodyObject = deadBody.GetDeadBody();

            deadPlayer.RpcTeleport(deadBodyObject.transform.position);
            deadPlayer.RpcRevive();
            HasGrail = false;
            return false;
        }
        return true;
    }
    public override bool KnowRoleTarget(PlayerControl seer, PlayerControl target) => seer.Is(CustomRoles.Archaeologist) && SlateRoles.Contains(target.PlayerId);

    public void RandomAntique()
    {
        var player = _Player;
        if (!player.IsAlive() || player == null) return;

        var rand = IRandom.Instance;
        AntiqueID = (byte)rand.Next(1, 19);
        FixNextSabo = false;
        HasGrail = false;
        LifeConnection = false;

        switch (AntiqueID)
        {
            case 1:
                player.Notify(GetString("GotMirror"), 15f);
                break;
            case 2:
                player.Notify(GetString("GotGrail"), 15f);
                break;
            case 3:
                player.Notify(GetString("GotCloak"), 15f);
                break;
            case 4:
                player.Notify(GetString("GotFlagstone"), 15f);
                break;
            case 5:
                player.Notify(GetString("GotOrbs"), 15f);
                break;
            case 6:
                player.Notify(GetString("GotBugle"), 15f);
                break;
            case 7:
                player.Notify(GetString("GotReel"), 15f);
                break;
            case 8:
                player.Notify(GetString("GotCrystal"), 15f);
                break;
            case 9:
                player.Notify(GetString("GotRelic"), 15f);
                break;
            case 10:
                player.Notify(GetString("GotIndenture"), 15f);
                break;
            case 11:
                player.Notify(GetString("GotDagger"), 15f);
                break;
            case 12:
                player.Notify(GetString("GotTalisman"), 15f);
                break;
            case 13:
                player.Notify(GetString("GotTruth"), 15f);
                break;
            case 14:
                player.Notify(GetString("GotHourglass"), 15f);
                break;
            case 15:
                player.Notify(GetString("GotProphecy"), 15f);
                break;
            case 16:
                player.Notify(GetString("GetButton"), 15f);
                break;
            case 17:
                player.Notify(GetString("GetKey"), 15f);
                break;
            case 18:
                player.Notify(GetString("GetBell"), 15f);
                break;
            default:
                break;
        }

        SendRPC(player);
    }

    public static void FixAllSabotage()
    {
        if (!FixNextSabo || CurrentActiveSabotage == SystemTypes.Hallway) return;
        FixNextSabo = false;
        var shipStatusDisabled = ShipStatus.Instance;
        switch (CurrentActiveSabotage)
        {
            case SystemTypes.Reactor:
            case SystemTypes.Laboratory:
                shipStatusDisabled.RpcUpdateSystem(CurrentActiveSabotage, 16);
                break;
            case SystemTypes.HeliSabotage:
                shipStatusDisabled.RpcUpdateSystem(CurrentActiveSabotage, 16);
                shipStatusDisabled.RpcUpdateSystem(CurrentActiveSabotage, 17);
                break;
            case SystemTypes.LifeSupp:
                shipStatusDisabled.RpcUpdateSystem(CurrentActiveSabotage, 66);
                shipStatusDisabled.RpcUpdateSystem(CurrentActiveSabotage, 67);
                break;
            case SystemTypes.Comms:
                var mapId = Utils.GetActiveMapId();

                shipStatusDisabled.RpcUpdateSystem(CurrentActiveSabotage, 16);
                if (mapId is 1 or 5)
                    shipStatusDisabled.RpcUpdateSystem(CurrentActiveSabotage, 17);
                break;
            case SystemTypes.Electrical:
                var __instance = ShipStatus.Instance.Systems[SystemTypes.Electrical].CastFast<SwitchSystem>();
                __instance.ActualSwitches = 0;
                __instance.ExpectedSwitches = 0;
                __instance.IsDirty = true;

                Logger.Info($"Archaeologist instant - fix-lights", "SwitchSystem");
                break;
        }
        CurrentActiveSabotage = SystemTypes.Hallway;
    }

    public static void OnSabotageCall(SystemTypes systemType)
    {
        if (!Main.MeetingIsStarted
            && systemType is
                SystemTypes.HeliSabotage or
                SystemTypes.Laboratory or
                SystemTypes.Reactor or
                SystemTypes.LifeSupp or
                SystemTypes.Comms or
                SystemTypes.Electrical)
        {
            CurrentActiveSabotage = systemType;
        }
    }
}
