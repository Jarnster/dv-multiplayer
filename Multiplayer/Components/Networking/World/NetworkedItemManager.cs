using System.Collections.Generic;
using System.Linq;
using DV.Utils;
using UnityEngine;
using JetBrains.Annotations;
using Multiplayer.Networking.Data;
using Multiplayer.Components.Networking.World;
using System;
using Multiplayer.Utils;
using DV.CabControls.Spec;

namespace Multiplayer.Components.Networking.Train;

public class NetworkedItemManager : SingletonBehaviour<NetworkedItemManager>
{
    private List<ItemUpdateData> DirtyItems = new List<ItemUpdateData>();
    private Queue<ItemUpdateData> ReceivedSnapshots = new Queue<ItemUpdateData>();
    private Dictionary<string, List<NetworkedItem>> CachedItems = new Dictionary<string, List<NetworkedItem>>();

protected override void Awake()
    {
        base.Awake();
        if (!NetworkLifecycle.Instance.IsHost())
            return;

    }

    protected void Start()
    {
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

        foreach (var snapshot in snapshots)
        {
            ReceivedSnapshots.Enqueue(snapshot);
        }

        Multiplayer.LogDebug(() => $"ReceiveSnapshots: {ReceivedSnapshots.Count}");
    }

    #region Common

    private void Common_OnTick(uint tick)
    {
        //Process received Snapshots
        ProcessReceived();

        if (NetworkLifecycle.Instance.IsHost())
            ProcessChanged();
    }

    private void ProcessReceived()
    {
        while (ReceivedSnapshots.Count > 0)
        {
            ItemUpdateData snapshot = ReceivedSnapshots.Dequeue();
            try
            {
                //Multiplayer.LogDebug(() => $"ProcessReceived: {snapshot.UpdateType}");

                if (snapshot == null || snapshot.UpdateType == ItemUpdateData.ItemUpdateType.None)
                {
                    Multiplayer.LogError($"NetworkedItemManager.ProcessReceived() Invalid Update Type: {snapshot?.UpdateType}, ItemNetId: {snapshot?.ItemNetId}, prefabName: {snapshot?.PrefabName}");
                    continue;
                }

                if (NetworkLifecycle.Instance.IsHost())
                {
                    ProcessReceivedAsHost(snapshot);
                }
                else
                {
                    ProcessReceivedAsClient(snapshot);
                }
            }
            catch (Exception ex)
            {
                Multiplayer.LogError($"NetworkedItemManager.ProcessReceived() Error! {ex.Message}\r\n{ex.StackTrace}");
            }
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

    #endregion

    #region Server

    private void ProcessReceivedAsHost(ItemUpdateData snapshot)
    {
        if (snapshot.UpdateType == ItemUpdateData.ItemUpdateType.Create)
        {
            Multiplayer.LogError($"NetworkedItemManager.ProcessReceived() Host received Create snapshot! ItemNetId: {snapshot.ItemNetId}, prefabName: {snapshot.PrefabName}");
            return;
        }

        if (NetworkedItem.Get(snapshot.ItemNetId, out NetworkedItem netItem))
        {
            if (ValidatePlayerAction(snapshot)) //Ensure the player can do this
            {
                netItem.ReceiveSnapshot(snapshot);
            }
            else
            {
                Multiplayer.LogWarning($"NetworkedItemManager.ProcessReceived() Player action validation failed for ItemNetId: {snapshot.ItemNetId}");
            }
        }
        else
        {
            Multiplayer.LogError($"NetworkedItemManager.ProcessReceived() NetworkedItem not found! Update Type: {snapshot.UpdateType}, ItemNetId: {snapshot.ItemNetId}, prefabName: {snapshot.PrefabName}");
        }
    }

    private bool ValidatePlayerAction(ItemUpdateData snapshot)
    {
        return true; // Placeholder
    }

    #endregion

    #region Client
    private void ProcessReceivedAsClient(ItemUpdateData snapshot)
    {
        if (snapshot.UpdateType == ItemUpdateData.ItemUpdateType.Create)
        {
            CreateItem(snapshot);
        }
        else if (NetworkedItem.Get(snapshot.ItemNetId, out NetworkedItem netItem))
        {
            netItem.ReceiveSnapshot(snapshot);
        }
        else
        {
            Multiplayer.LogError($"NetworkedItemManager.ProcessReceived() NetworkedItem not found on client! Update Type: {snapshot.UpdateType}, ItemNetId: {snapshot.ItemNetId}, prefabName: {snapshot.PrefabName}");
        }
    }
    #endregion

    #region Item Cache And Management
    private void CreateItem(ItemUpdateData snapshot)
    {
        if(snapshot == null || snapshot.ItemNetId == 0)
        {
            Multiplayer.LogError($"NetworkedItemManager.CreateItem() Invalid snapshot! ItemNetId: {snapshot?.ItemNetId}, prefabName: {snapshot?.PrefabName}");
            return;
        }

        NetworkedItem newItem = GetFromCache(snapshot.PrefabName);

        if(newItem == null)
        {
            GameObject prefabObj = Resources.Load(snapshot.PrefabName) as GameObject;

            if (prefabObj == null)
            {
                Multiplayer.LogError($"NetworkedItemManager.CreateItem() Unable to load prefab for ItemNetId: {snapshot.ItemNetId}, prefabName: {snapshot.PrefabName}");
                return;
            }

            //create a new item
            GameObject gameObject = Instantiate(prefabObj, snapshot.PositionData.Position, snapshot.PositionData.Rotation);

            //Make sure we have a NetworkedItem
            newItem = gameObject.GetOrAddComponent<NetworkedItem>();
        }

        newItem.gameObject.SetActive(true);

        //InventoryItemSpec component = newItem.GetComponent<InventoryItemSpec>();
        //if (newItem.Item.InventorySpecs != null)
        //    newItem.Item.InventorySpecs.BelongsToPlayer = false;

        //SingletonBehaviour<StorageController>.Instance.AddItemToWorldStorage(newItem.Item);

        newItem.NetId = snapshot.ItemNetId;
        newItem.ReceiveSnapshot(snapshot);
    }

    public void CacheWorldItems()
    {
        if (NetworkLifecycle.Instance.IsHost())
            return;

        NetworkLifecycle.Instance.Client.LogDebug(() => $"CacheWorldItems()");

        // Remove all spawned world items and place them into a cache for later use
        var items = NetworkedItem.GetAll().ToList();
        foreach (var item in items)
        {
            try
            {
                if (item.Item != null && !item.Item.IsEssential() && !item.Item.IsGrabbed() && !StorageController.Instance.StorageInventory.ContainsItem(item.Item))
                {
                    SendToCache(item);
                }
                else
                {
                    NetworkLifecycle.Instance.Client.LogDebug(() => $"CacheWorldItems() Not caching: {item.Item.InventorySpecs.previewPrefab} is in Inventory: {StorageController.Instance.StorageInventory.ContainsItem(item.Item)}");
                }
            }
            catch (Exception ex)
            {
                NetworkLifecycle.Instance.Client.LogDebug(() => $"Error Caching Spawned Item: {ex.Message}");
            }
        }
    }

    private NetworkedItem GetFromCache(string prefabName)
    {
        if (CachedItems.TryGetValue(prefabName, out var items) && items.Count > 0)
        {
            //NetworkLifecycle.Instance.Client.LogDebug(() => $"GetFromCache({prefabName}) Cache Hit");
            var cachedItem = items[items.Count - 1];
            items.RemoveAt(items.Count - 1);
            return cachedItem;
        }

        //NetworkLifecycle.Instance.Client.LogDebug(() => $"GetFromCache({prefabName}) Cache Miss!");
        return null;
    }

    private void SendToCache(NetworkedItem netItem)
    {
        string prefabName = netItem?.Item?.InventorySpecs?.itemPrefabName;

        NetworkLifecycle.Instance.Client.LogDebug(() => $"Caching Spawned Item: {prefabName ?? ""}");

        netItem.BlockSync = true;

        netItem.gameObject.SetActive(false);
        RespawnOnDrop respawn = netItem.Item.GetComponent<RespawnOnDrop>();

        Destroy(respawn);
        

        NetworkLifecycle.Instance.Client.LogDebug(() => $"Caching Spawned Item: {prefabName ?? ""}: checkWhileDisabled {respawn.checkWhileDisabled}, ignoreDistanceFromSpawnPosition {respawn.ignoreDistanceFromSpawnPosition}, respawnOnDropThroughFloor {respawn.respawnOnDropThroughFloor}");


        //respawn.checkWhileDisabled = false;
        //respawn.ignoreDistanceFromSpawnPosition = true;
        //respawn.respawnOnDropThroughFloor = false;
        //netItem.Item.itemDisabler.ToggleInDumpster(false);

        if (SingletonBehaviour<StorageController>.Instance.StorageWorld.ContainsItem(netItem.Item))
        {
            SingletonBehaviour<StorageController>.Instance.RemoveItemFromWorldStorage(netItem.Item);
        }

        netItem.Item.InventorySpecs.BelongsToPlayer = false;
        netItem.NetId = 0;
        

        
        if (!CachedItems.ContainsKey(prefabName))
        {
            CachedItems[prefabName] = new List<NetworkedItem>();
        }
        CachedItems[prefabName].Add(netItem);
    }

    #endregion




    [UsedImplicitly]
    public new static string AllowAutoCreate()
    {
        return $"[{nameof(NetworkedItemManager)}]";
    }
}
