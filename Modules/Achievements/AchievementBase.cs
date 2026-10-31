using System;
using static TONE.Translator;

namespace TONE;

public abstract class AchievementBase
{
    public abstract int Id { get; }

    public abstract string Name { get; }

    public string Title => GetString($"Achievement.Title.{Name}");

    public string Description => GetString($"Achievement.Desc.{Name}");

    public abstract CustomRoles Role { get; }

    public abstract int TargetProgress { get; }

    public int Progress { get; private set; }

    public virtual bool SaveProgress => true;

    public bool IsCompleted => Progress >= TargetProgress;

    public bool CompletedThisGame { get; internal set; }

    public void AddProgress(int amount = 1)
    {
        if (IsCompleted || amount <= 0 || GameStates.IsLocalGame || Options.NoGameEnd.GetBool()) return;
        Progress = Math.Min(Progress + amount, TargetProgress);
        if (IsCompleted) AchievementManager.OnAchievementCompleted(this);
    }

    public void SetProgress(int progress, bool completed)
    {
        Progress = Math.Clamp(progress, 0, TargetProgress);
        if (completed) Progress = TargetProgress;
    }

    public void ResetProgress() => Progress = 0;

    protected bool IsMyRole(PlayerControl player) => player && player.GetCustomRole() == Role;

    public virtual void OnGameStart()
    { }

    public virtual void OnPlayerKilled(PlayerControl killer, PlayerControl target)
    { }

    public virtual void OnGameEnd(bool isWinner)
    { }

    public virtual string GetProgressText() => $"({Progress}/{TargetProgress})";

    public virtual void OnRoleAbility(AchievementEventType type, PlayerControl player)
    { }

    protected static bool IsLocalPlayer(PlayerControl player) => player && player.IsHost();

    public enum AchievementEventType
    {
        BlackmailerBlackmailed,
        BountyHunterKilledTarget,
        BountyHunterKilledNonTarget,
        CleanerCleanedBody,
        SheriffMisfire,
        VeteranKilledAttacker,
        PelicanEat,
    }
}