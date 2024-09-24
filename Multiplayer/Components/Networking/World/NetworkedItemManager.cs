using System.Collections.Generic;
using System.Linq;
using DV.Utils;
using UnityEngine;
using JetBrains.Annotations;
using Multiplayer.Networking.Data;
using Multiplayer.Networking.Packets.Clientbound.Train;
using Multiplayer.Utils;
using Multiplayer.Components.Networking.World;

namespace Multiplayer.Components.Networking.Train;

public class NetworkedItemManager : SingletonBehaviour<NetworkedItemManager>
{
    private List<ItemUpdateData> DirtyItems = new List<ItemUpdateData>();

    protected override void Awake()
    {
        base.Awake();
        if (!NetworkLifecycle.Instance.IsHost())
            return;

        NetworkLifecycle.Instance.OnTick += Common_OnTick;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (UnloadWatcher.isQuitting)
            return;

        NetworkLifecycle.Instance.OnTick -= Common_OnTick;
    }

    public void AddDirtyItemSnapshot(ItemUpdateData item)
    {
        if(! DirtyItems.Contains(item))
            DirtyItems.Add(item);
    }

    #region Common

    private void Common_OnTick(uint tick)
    {
        if(DirtyItems.Count == 0)
            return;

        if(NetworkLifecycle.Instance.IsHost())
        {
            NetworkLifecycle.Instance.Server.SendItemsChangePacket(DirtyItems);
        }
        else
        {
            NetworkLifecycle.Instance.Client.SendItemsChangePacket(DirtyItems);
        }

        DirtyItems.Clear();
    }
    #endregion

    [UsedImplicitly]
    public new static string AllowAutoCreate()
    {
        return $"[{nameof(NetworkedItemManager)}]";
    }
}
