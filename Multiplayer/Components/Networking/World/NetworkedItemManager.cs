using System.Collections.Generic;
using System.Linq;
using DV.Utils;
using UnityEngine;
using JetBrains.Annotations;
using Multiplayer.Networking.Data;
using Multiplayer.Components.Networking.World;

namespace Multiplayer.Components.Networking.Train;

public class NetworkedItemManager : SingletonBehaviour<NetworkedItemManager>
{
    private List<ItemUpdateData> DirtyItems = new List<ItemUpdateData>();
    private List<ItemUpdateData> ReceivedSnapshots = new List<ItemUpdateData>();

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

    public void ReceiveSnapshots(List<ItemUpdateData> snapshots)
    {
        if (snapshots == null)
            return;

        ReceivedSnapshots.AddRange(snapshots);
    }

    #region Common

    private void Common_OnTick(uint tick)
    {
        //Process received Snapshots
        ProcessReceived();

        ProcessChanged();
    }

    private void ProcessReceived()
    {
        while(ReceivedSnapshots.Count > 0)
        {
            ItemUpdateData snapshot = ReceivedSnapshots.First();

            //process
            if (snapshot != null && snapshot.UpdateType != ItemUpdateData.ItemUpdateType.None)
            {
                //try to find an existing item
                NetworkedItem.Get(snapshot.ItemNetId, out NetworkedItem netItem);

                if (NetworkLifecycle.Instance.IsHost())
                {
                    if (snapshot.UpdateType == ItemUpdateData.ItemUpdateType.Create)
                    {
                        Multiplayer.LogError($"NetworkedItemManager.ProcessReceived() Host received Create snapshot! ItemNetId: {snapshot.ItemNetId}, prefabName: {snapshot.PrefabName}");
                    }
                    else
                    {
                        //we should validate if the player can perform this action... TODO later
                        if (netItem != null)
                            netItem.ReceiveSnapshot(snapshot);
                        else
                            Multiplayer.LogError($"NetworkedItemManager.ProcessReceived() NetworkedItem not found! Update Type: {snapshot?.UpdateType}, ItemNetId: {snapshot?.ItemNetId}, prefabName: {snapshot?.PrefabName}");
                    }
                }
                else
                {
                    if (snapshot.UpdateType == ItemUpdateData.ItemUpdateType.Create)
                    {
                        CreateItem(snapshot);
                    }
                    else
                    {
                        netItem.ReceiveSnapshot(snapshot);
                    }
                }
            }
            else
            {
                Multiplayer.LogError($"NetworkedItemManager.ProcessReceived() Invalid Update Type: {snapshot?.UpdateType}, ItemNetId: {snapshot?.ItemNetId}, prefabName: {snapshot?.PrefabName}");
            }

            ReceivedSnapshots.Remove(snapshot);
        }
    }

    private void ProcessChanged()
    {
        //Process all items for updates
        foreach (var item in NetworkedItem.GetAll())
        {
            ItemUpdateData snapshot = item.GetSnapshot();

            if (snapshot != null)
                DirtyItems.Add(snapshot);
        }

        if (DirtyItems.Count == 0)
            return;

        if (NetworkLifecycle.Instance.IsHost())
        {
            NetworkLifecycle.Instance.Server.SendItemsChangePacket(DirtyItems);
        }
        else
        {
            NetworkLifecycle.Instance.Client.SendItemsChangePacket(DirtyItems);
        }

        DirtyItems.Clear();
    }

    private void CreateItem(ItemUpdateData snapshot)
    {
        if(snapshot == null || snapshot.ItemNetId == 0)
        {
            Multiplayer.LogError($"NetworkedItemManager.CreateItem() Invalid snapshot! ItemNetId: {snapshot?.ItemNetId}, prefabName: {snapshot?.PrefabName}");
            return;
        }

        GameObject prefabObj = Resources.Load(snapshot.PrefabName) as GameObject;

        if (prefabObj == null)
        {
            Multiplayer.LogError($"NetworkedItemManager.CreateItem() Unable to load prefab for ItemNetId: {snapshot.ItemNetId}, prefabName: {snapshot.PrefabName}");
            return;
        }

        //create a new item
        GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(prefabObj, snapshot.PositionData.Position, snapshot.PositionData.Rotation);

        InventoryItemSpec component = gameObject.GetComponent<InventoryItemSpec>();
        if (component != null)
            component.BelongsToPlayer = true;
 
        NetworkedItem newItem = gameObject.AddComponent<NetworkedItem>();
        newItem.NetId = snapshot.ItemNetId;
        newItem.ReceiveSnapshot(snapshot);
    }

    #endregion

    [UsedImplicitly]
    public new static string AllowAutoCreate()
    {
        return $"[{nameof(NetworkedItemManager)}]";
    }
}
