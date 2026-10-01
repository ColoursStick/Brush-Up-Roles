using AmongUs.GameOptions;
using static TONE.Options;

namespace TONE.Roles.Vanilla;

/// <summary>
/// 网红 —— 原版 v19 新增的幽灵职业（游戏内部名 SpiritGuide）。
///
/// 玩法：船员死亡后成为网红，可用「图片卡片」向存活玩家传递信息。
///      卡片的收发逻辑游戏本体自带，模组只需要：
///        · 把职业发出去（ThisRoleBase = GuardianAngel，作为原版幽灵职业的载体）
///        · 提供一个冷却设置
///
/// 之所以能这么薄：网红是纯客户端行为（卡片从幽灵客户端直接发给目标），
/// 房主只负责分配职业，不需要额外的 RPC 或状态同步。
/// </summary>
internal class Influencer : RoleBase
{
    //===========================SETUP================================\\
    public override CustomRoles Role => CustomRoles.Influencer;
    private const int Id = 34300;

    /// <summary>作为原版幽灵职业的载体（网红没有专属的原版 RoleTypes 基类可用）</summary>
    public override CustomRoles ThisRoleBase => CustomRoles.GuardianAngel;

    /// <summary>归到「船员 · 幽灵职业」，与守护天使等一起显示</summary>
    public override Custom_RoleType ThisRoleType => Custom_RoleType.CrewmateGhosts;
    //==================================================================\\

    private static OptionItem AbilityCooldown;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Influencer);

        AbilityCooldown = FloatOptionItem.Create(Id + 10, "InfluencerCooldown", new(5f, 120f, 5f), 30f,
                TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Influencer])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        // 复用守护天使的冷却字段作为技能冷却的载体
        AURoleOptions.GuardianAngelCooldown = AbilityCooldown.GetFloat();
    }
}
