using AmongUs.GameOptions;
using TONE.Modules;
using TONE.Roles.Crewmate;
using static TONE.Options;

namespace TONE.Roles.Neutral;

internal class SerialKiller : RoleBase
{

    public override CustomRoles Role => CustomRoles.SerialKiller;
    private const int Id = 17900;
    public override bool IsDesyncRole => true;
    public override CustomRoles ThisRoleBase => CustomRoles.Impostor;
    public override Custom_RoleType ThisRoleType => Custom_RoleType.NeutralKilling;

    private static OptionItem KillCooldown;
    private static OptionItem Kill2Duration;
    private static OptionItem CanVent;
    private static OptionItem HasImpostorVision;

    private bool CanKill2;

    public override void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.SerialKiller, 1, zeroOne: false);
        KillCooldown = FloatOptionItem.Create(Id + 10, GeneralOption.KillCooldown, new(0f, 180f, 2.5f), 30f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.SerialKiller])
            .SetValueFormat(OptionFormat.Seconds);
        Kill2Duration = FloatOptionItem.Create(Id + 11, "Kill2Duration", new(0f, 180f, 2.5f), 5f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.SerialKiller])
            .SetValueFormat(OptionFormat.Seconds);
        CanVent = BooleanOptionItem.Create(Id + 12, GeneralOption.CanVent, true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.SerialKiller]);
        HasImpostorVision = BooleanOptionItem.Create(Id + 13, GeneralOption.ImpostorVision, true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.SerialKiller]);

    }

    public override void Init()
    {
        CanKill2 = false;
    }

    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    public override void ApplyGameOptions(IGameOptions opt, byte id) => opt.SetVision(HasImpostorVision.GetBool());
    public override bool CanUseKillButton(PlayerControl pc) => true;
    public override bool CanUseImpostorVentButton(PlayerControl pc) => CanVent.GetBool();

    public override void OnMurderPlayerAsKiller(PlayerControl killer, PlayerControl target, bool inMeeting, bool isSuicide)
    {
        if (target.Is(CustomRoles.President) && President.CheckPresidentReveal[target.PlayerId]) return;
        if (!CanKill2)
        {
            CanKill2 = true;
            killer.SetKillCooldown(0.1f);

            _ = new LateTask(() =>
            {
                if (killer.GetKillTimer() > 0) return;
                CanKill2 = false;
                killer.ResetKillCooldown();
            }, Kill2Duration.GetFloat(), "Serial Killer Kill2 Duration");
        }
        else CanKill2 = false;
    }
}
