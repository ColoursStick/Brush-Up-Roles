using Hazel;
using System;
using TONE.Modules.Rpc;
using UnityEngine;

namespace TONE;

static class TargetArrow
{
    static readonly Dictionary<ArrowInfo, string> TargetArrows = [];

    static readonly string[] Arrows =
    [
        "↑",
        "↗",
        "→",
        "↘",
        "↓",
        "↙",
        "←",
        "↖",
        "・"
    ];

    public static void Init()
    {
        TargetArrows.Clear();
    }

    public static void SendRPC(int index, byte seerId, byte targetId = byte.MaxValue)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        var seer = seerId.GetPlayer();
        if (!seer.IsNonHostModdedClient()) return;

        var message = new RpcArrow(PlayerControl.LocalPlayer.NetId, true, index, seerId, targetId, null);
        RpcUtils.LateSpecificSendMessage(message, seer.OwnerId);
    }
    public static void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                Add(reader.ReadByte(), reader.ReadByte());
                break;
            case 2:
                Remove(reader.ReadByte(), reader.ReadByte());
                break;
            case 3:
                RemoveAllTarget(reader.ReadByte());
                break;
        }
    }

    public static void Add(byte seer, byte target)
    {
        var arrowInfo = new ArrowInfo(seer, target);
        if (!TargetArrows.Any(a => a.Key.Equals(arrowInfo)))
        {
            TargetArrows[arrowInfo] = "・";
            SendRPC(1, seer, target);
            Logger.Info($"New target arrow: {seer} ({seer.GetPlayer()?.GetRealName()}) => {target} ({target.GetPlayer()?.GetRealName()})", "TargetArrow");
        }
    }

    public static void Remove(byte seer, byte target)
    {
        var arrowInfo = new ArrowInfo(seer, target);
        var removeList = new List<ArrowInfo>(TargetArrows.Keys.Where(k => k.Equals(arrowInfo)));
        if (!removeList.Any()) return;

        foreach (ArrowInfo a in removeList.ToArray())
        {
            TargetArrows.Remove(a);
        }

        SendRPC(2, seer, target);
        Logger.Info($"Removed target arrow: {seer} ({seer.GetPlayer()?.GetRealName()}) => {target} ({target.GetPlayer()?.GetRealName()})", "TargetArrow");
    }

    public static void RemoveAllTarget(byte seer)
    {
        var removeList = new List<ArrowInfo>(TargetArrows.Keys.Where(k => k.From == seer));
        if (!removeList.Any()) return;

        foreach (ArrowInfo arrowInfo in removeList.ToArray())
        {
            TargetArrows.Remove(arrowInfo);
        }
        SendRPC(3, seer);
        Logger.Info($"Removed all target arrows for {seer} ({seer.GetPlayer()?.GetRealName()})", "TargetArrow");
    }

    public static string GetArrows(PlayerControl seer, params byte[] targets)
    {
        return TargetArrows.Keys.Where(ai => ai.From == seer.PlayerId && targets.Contains(ai.To)).Aggregate(string.Empty, (current, arrowInfo) => current + TargetArrows[arrowInfo]) ?? string.Empty;
    }
    public static string GetArrows(PlayerControl seer)
    {
        return TargetArrows.Keys.Where(ai => ai.From == seer.PlayerId).Aggregate(string.Empty, (current, arrowInfo) => current + TargetArrows[arrowInfo]) ?? string.Empty;
    }

    public static string GetAllArrows(PlayerControl seer)
    {
        return TargetArrows.Keys.Where(ai => ai.From == seer.PlayerId).Aggregate(string.Empty, (current, arrowInfo) => current + TargetArrows[arrowInfo]) ?? string.Empty;
    }

    public static void OnFixedUpdate(PlayerControl seer)
    {
        if (!GameStates.IsInTask) return;

        var seerId = seer.PlayerId;
        var seerIsDead = !seer.IsAlive();

        var arrowList = new List<ArrowInfo>(TargetArrows.Keys.Where(a => a.From == seerId));
        if (!arrowList.Any()) return;

        var update = false;
        foreach (var arrowInfo in arrowList.ToArray())
        {
            var targetId = arrowInfo.To;
            var target = targetId.GetPlayer();
            if (seerIsDead || (!target.IsAlive() && !seer.Is(CustomRoles.Spiritualist)))
            {
                TargetArrows.Remove(arrowInfo);
                update = true;
                continue;
            }

            var dir = target.transform.position - seer.transform.position;
            int index;
            if (dir.magnitude < 2)
            {

                index = 8;
            }
            else
            {

                var angle = Vector3.SignedAngle(Vector3.down, dir, Vector3.back) + 180 + 22.5;
                index = ((int)(angle / 45)) % 8;
            }

            var arrow = Arrows[index];
            if (TargetArrows[arrowInfo] != arrow)
            {
                TargetArrows[arrowInfo] = arrow;
                update = true;
            }
        }

        if (update && (!seer.IsModded()))
        {
            Utils.NotifyRoles(SpecifySeer: seer, ForceLoop: false);
        }
    }

    class ArrowInfo(byte from, byte to)
    {
        public readonly byte From = from;
        public readonly byte To = to;

        public bool Equals(ArrowInfo obj)
        {
            return From == obj.From && To == obj.To;
        }

        public override string ToString()
        {
            return $"(From:{From} To:{To})";
        }
    }
}
