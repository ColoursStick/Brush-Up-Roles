using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace TONE.Modules;

public static class AdminProvider
{

    public static SortedDictionary<SystemTypes, AdminEntry> CalculateAdmin()
    {
        SortedDictionary<SystemTypes, AdminEntry> allAdmins = [];

        HashSet<int> countedPlayers = new(15);

        Il2CppReferenceArray<Collider2D> colliders = new(45);

        ContactFilter2D filter = new()
        {
            useLayerMask = true,
            layerMask = Constants.LivingPlayersOnlyMask,
            useTriggers = true,
        };

        foreach (var room in ShipStatus.Instance.AllRooms)
        {
            var roomId = room.RoomId;

            if (roomId == SystemTypes.Hallway || room.roomArea == null)
            {
                continue;
            }

            var numColliders = room.roomArea.OverlapCollider(filter, colliders);

            var totalPlayers = 0;
            var numDeadBodies = 0;
            var numImpostors = 0;

            for (var i = 0; i < numColliders; i++)
            {
                var collider = colliders[i];

                if (collider.CompareTag("DeadBody"))
                {
                    var deadBody = collider.GetComponent<DeadBody>();
                    if (deadBody != null && countedPlayers.Add(deadBody.ParentId))
                    {
                        totalPlayers++;
                        numDeadBodies++;

                        if (deadBody.ParentId.GetPlayer()?.Is(Custom_Team.Impostor) == true)
                        {
                            numImpostors++;
                        }
                    }
                }

                else if (!collider.isTrigger)
                {
                    var playerControl = collider.GetComponent<PlayerControl>();
                    if (playerControl.IsAlive() && countedPlayers.Add(playerControl.PlayerId))
                    {
                        totalPlayers++;

                        if (playerControl.Is(Custom_Team.Impostor))
                        {
                            numImpostors++;
                        }
                    }
                }
            }

            allAdmins[roomId] = new()
            {
                Room = roomId,
                TotalPlayers = totalPlayers,
                NumDeadBodies = numDeadBodies,
                NumImpostors = numImpostors,
            };
        }
        return allAdmins;
    }

    public readonly record struct AdminEntry
    {

        public SystemTypes Room { get; init; }

        public int TotalPlayers { get; init; }

        public int NumDeadBodies { get; init; }

        public int NumImpostors { get; init; }
    }
}
