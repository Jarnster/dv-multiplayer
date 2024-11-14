using DV.CabControls;
using DV.Interaction;
using DV.InventorySystem;
using DV.Items;
using Multiplayer.Components.Networking.Train;
using Multiplayer.Networking.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Multiplayer.Components.Networking.World;

public enum ItemState : byte
{
    Dropped,        //belongs to the world
    Thrown,         //was thrown by player
    InHand,         //held by player
    InInventory,    //in player's inventory
    Attached        //attached to another object (e.g. EOT Lanterns)
}

public class NetworkedItem : IdMonoBehaviour<ushort, NetworkedItem>
{
    #region Lookup Cache
    private static readonly Dictionary<ItemBase, NetworkedItem> itemBaseToNetworkedItem = new();

    public static List<NetworkedItem> GetAll()
    {
        return itemBaseToNetworkedItem.Values.ToList();
    }
    public static bool Get(ushort netId, out NetworkedItem obj)
    {
        bool b = Get(netId, out IdMonoBehaviour<ushort, NetworkedItem> rawObj);
        obj = (NetworkedItem)rawObj;
        return b;
    }

    public static bool GetItem(ushort netId, out ItemBase obj)
    {
        bool b = Get(netId, out NetworkedItem networkedItem);
        obj = b ? networkedItem.Item : null;
        return b;
    }

    public static bool TryGetNetworkedItem(ItemBase item, out NetworkedItem networkedItem)
    {
        return itemBaseToNetworkedItem.TryGetValue(item, out networkedItem);
    }
    #endregion

    private const float PositionThreshold = 0.1f;
    private const float RotationThreshold = 0.1f;

    public ItemBase Item { get; private set; }
    private GrabHandlerItem GrabHandler;
    private SnappableItem SnappableItem;
    private Component trackedItem;
    private List<object> trackedValues = new List<object>();
    public bool UsefulItem { get; private set; } = false;
    public Type TrackedItemType { get; private set; }
    public bool BlockSync { get; set; } = false;
    public uint LastDirtyTick { get; private set; }
    private bool Initialised;

    //Track dirty states
    private bool CreatedDirty = true;   //if set, we created this item dirty and have not sent an update

    private ItemState lastState;
    private bool stateDirty;
    private bool wasThrown;

    private Vector3 thrownPosition;
    private Quaternion thrownRotation;
    private Vector3 throwDirection;

    //Handle ownership
    public ushort OwnerId { get; private set; } = 0; // 0 means no owner

    //public void SetOwner(ushort playerId)
    //{
    //    if (OwnerId != playerId)
    //    {
    //        if (OwnerId != 0)
    //        {
    //            NetworkedItemManager.Instance.RemoveItemFromPlayerInventory(this);
    //        }
    //        OwnerId = playerId;
    //        if (playerId != 0)
    //        {
    //            NetworkedItemManager.Instance.AddItemToPlayerInventory(playerId, this);
    //        }
    //    }
    //}

    protected override bool IsIdServerAuthoritative => true;

    protected override void Awake()
    {
        base.Awake();
        Multiplayer.LogDebug(() => $"NetworkedItem.Awake() {name}");
        NetworkedItemManager.Instance.CheckInstance(); //Ensure the NetworkedItemManager is initialised

        Register();
    }

    protected void Start()
    {
        if (!CreatedDirty)
            return;
    }

    public T GetTrackedItem<T>() where T : Component
    {
        return UsefulItem ? trackedItem as T : null;
    }

    public void Initialize<T>(T item, ushort netId = 0, bool createDirty = true) where T : Component
    {
        if(netId != 0)
            NetId = netId;

        trackedItem = item;
        TrackedItemType = typeof(T);
        UsefulItem = true;

        CreatedDirty = createDirty;

        if(Item == null)
            Register();

    }

    private bool Register()
    {
        if (Initialised)
            return false;

        try
        {

            if (!TryGetComponent(out ItemBase itemBase))
            {
                Multiplayer.LogError($"Unable to find ItemBase for {name}");
                return false;
            }

            Item = itemBase;
            itemBaseToNetworkedItem[Item] = this;

            Item.Grabbed += OnGrabbed;
            Item.Ungrabbed += OnUngrabbed;

            TryGetComponent<GrabHandlerItem>(out GrabHandler);
            TryGetComponent<SnappableItem>(out SnappableItem);


            //Item.ItemInventoryStateChanged += OnItemInventoryStateChanged;

            lastState = GetItemState();
            stateDirty = false;

            Initialised = true;
            return true;
        }
        catch (Exception ex)
        {
            Multiplayer.LogError($"Unable to find ItemBase for {name}\r\n{ex.Message}");
            return false; 
        }
    }

    private void OnUngrabbed(ControlImplBase obj)
    {
        Multiplayer.LogDebug(() => $"OnUngrabbed() NetID: {NetId}, {name}");
        stateDirty = true;
    }

    private void OnGrabbed(ControlImplBase obj)
    {
        Multiplayer.LogDebug(() => $"OnGrabbed() NetID: {NetId}, {name}");
        stateDirty = true;
    }

    public void OnThrow(Vector3 direction)
    {
        Multiplayer.LogDebug(() => $"OnThrow() netId: {NetId}, Name: {name}, Direction: {direction}");
        throwDirection = direction;
        thrownPosition = Item.transform.position - WorldMover.currentMove;
        thrownRotation = Item.transform.rotation;

        wasThrown = true;
        stateDirty = true;
    }


    #region Item Value Tracking
    public void RegisterTrackedValue<T>(string key, Func<T> valueGetter, Action<T> valueSetter)
    {
        trackedValues.Add(new TrackedValue<T>(key, valueGetter, valueSetter));
    }

    private bool HasDirtyValues()
    {
        return trackedValues.Any(tv => ((dynamic)tv).IsDirty);
    }

    private Dictionary<string, object> GetDirtyStateData()
    {
        var dirtyData = new Dictionary<string, object>();
        foreach (var trackedValue in trackedValues)
        {
            if (((dynamic)trackedValue).IsDirty)
            {
                dirtyData[((dynamic)trackedValue).Key] = ((dynamic)trackedValue).GetValueAsObject();
            }
        }
        return dirtyData;
    }

    private void MarkValuesClean()
    {
        foreach (var trackedValue in trackedValues)
        {
            ((dynamic)trackedValue).MarkClean();
        }
    }

    #endregion

    public ItemUpdateData GetSnapshot()
    {
        ItemUpdateData snapshot;
        ItemUpdateData.ItemUpdateType updateType = ItemUpdateData.ItemUpdateType.None;

        if (Item == null && Register() == false)
            return null;

        if (!stateDirty)
            return null;

        ItemState currentState = GetItemState();

        if (!CreatedDirty)
        {
            if(lastState != currentState)
                updateType |= ItemUpdateData.ItemUpdateType.ItemState;

            if (HasDirtyValues())
            {
                Multiplayer.LogDebug(GetDirtyValuesDebugString);
                updateType |= ItemUpdateData.ItemUpdateType.ObjectState;
            }
        }
        else
        {
            updateType = ItemUpdateData.ItemUpdateType.Create;
        }

        //no changes this snapshot
        if (updateType == ItemUpdateData.ItemUpdateType.None)
            return null;

        lastState = currentState;
        LastDirtyTick = NetworkLifecycle.Instance.Tick;
        snapshot = CreateUpdateData(updateType);

        CreatedDirty = false;
        stateDirty = false;
        wasThrown = false;

        MarkValuesClean();

        return snapshot;
    }

    public void ReceiveSnapshot(ItemUpdateData snapshot)
    {
        if(snapshot == null || snapshot.UpdateType == ItemUpdateData.ItemUpdateType.None)
            return;

        if (snapshot.UpdateType.HasFlag(ItemUpdateData.ItemUpdateType.ItemState) || snapshot.UpdateType.HasFlag(ItemUpdateData.ItemUpdateType.FullSync) || snapshot.UpdateType.HasFlag(ItemUpdateData.ItemUpdateType.Create))
        {
            Multiplayer.Log($"NetworkedItem.ReceiveSnapshot() netID: {snapshot?.ItemNetId}, ItemUpdateType {snapshot?.UpdateType}, ItemState {snapshot?.ItemState}");

            switch (snapshot.ItemState)
            {
                case ItemState.Dropped:
                    this.gameObject.SetActive(true);
                    transform.position = snapshot.ItemPosition + WorldMover.currentMove;
                    transform.rotation = snapshot.ItemRotation;
                    OwnerId = 0;
                    break;

                case ItemState.Thrown:
                    this.gameObject.SetActive(true);
                    transform.position = snapshot.ItemPosition + WorldMover.currentMove;
                    transform.rotation = snapshot.ItemRotation;
                    OwnerId = 0;

                    GrabHandler?.Throw(throwDirection);
                    break;

                case ItemState.InHand:
                    this.gameObject.SetActive(false);
                    break;

                case ItemState.InInventory:
                    this.gameObject.SetActive(false);
                    break;

                case ItemState.Attached:
                    this.gameObject.SetActive(true);
                    break;

                default:
                    throw new Exception($"Item state not implemented: {snapshot.ItemState}");

            }

        }

        Multiplayer.Log($"NetworkedItem.ReceiveSnapshot() netID: {snapshot?.ItemNetId}, ItemUpdateType {snapshot?.UpdateType} About to process states");

        if (snapshot.UpdateType.HasFlag(ItemUpdateData.ItemUpdateType.ObjectState) || snapshot.UpdateType.HasFlag(ItemUpdateData.ItemUpdateType.FullSync) || snapshot.UpdateType.HasFlag(ItemUpdateData.ItemUpdateType.Create))
        {
            //Multiplayer.Log($"NetworkedItem.ReceiveSnapshot() netID: {snapshot.ItemNetId}, States: {snapshot?.States?.Count}");

            if (snapshot.States != null)
            {
                foreach (var state in snapshot.States)
                {
                    var trackedValue = trackedValues.Find(tv => ((dynamic)tv).Key == state.Key);
                    if (trackedValue != null)
                    {
                        try
                        {
                            ((dynamic)trackedValue).SetValueFromObject(state.Value);
                            Multiplayer.LogDebug(() => $"Updated tracked value: {state.Key}");
                        }
                        catch (Exception ex)
                        {
                            Multiplayer.LogError($"Error updating tracked value {state.Key}: {ex.Message}");
                        }
                    }
                    else
                    {
                        Multiplayer.LogWarning($"Tracked value not found: {state.Key}");
                    }
                }
            }
        }

        Multiplayer.Log($"NetworkedItem.ReceiveSnapshot() netID: {snapshot?.ItemNetId}, ItemUpdateType {snapshot?.UpdateType} states processed");

        //mark values as clean
        CreatedDirty = false;
        stateDirty = false;

        MarkValuesClean();
        return;
    }

    public ItemUpdateData CreateUpdateData(ItemUpdateData.ItemUpdateType updateType)
    {
        Multiplayer.LogDebug(() => $"NetworkedItem.CreateUpdateData({updateType}) NetId: {NetId}, name: {name}");

        Vector3 position;
        Quaternion rotation;

        if (wasThrown)
        {
            position = thrownPosition;
            rotation = thrownRotation;
        }
        else
        {
            position = transform.position - WorldMover.currentMove;
            rotation = transform.rotation;
        }

        var updateData = new ItemUpdateData
        {
            UpdateType = updateType,
            ItemNetId = NetId,
            PrefabName = Item.InventorySpecs.ItemPrefabName,
            ItemState = lastState,
            ItemPosition = position,
            ItemRotation = rotation,
            ThrowDirection = throwDirection,
            States = GetDirtyStateData(),
        };

        return updateData;
    }

    private ItemState GetItemState()
    {
        Multiplayer.LogDebug(() => $"GetItemState() NetId: {NetId}, {name}, Parent: {Item.transform.parent} WorldMover: {WorldMover.OriginShiftParent}, wasThrown: {wasThrown}, isGrabbed: {Item.IsGrabbed()} Inventory.Contains(): {Inventory.Instance.Contains(this.gameObject, false)} Storage.Contains: {StorageController.Instance.StorageInventory.ContainsItem(Item)}");


        if (Item.transform.parent == WorldMover.OriginShiftParent)
            return ItemState.Dropped;

        if (wasThrown)
            return ItemState.Thrown;

        if (Item.IsGrabbed())
            return ItemState.InHand;

        if (Inventory.Instance.Contains(this.gameObject, false))
            return ItemState.InInventory;

        if(SnappableItem != null && SnappableItem.IsSnapped)
        {

            Multiplayer.LogDebug(() => $"GetItemState() NetId: {NetId}, {name}, snapped! {this.transform.parent}");
            return ItemState.Attached;
        }

        //we need a condition to check if it's attached to something else
        return ItemState.Dropped;
            
    }

    protected override void OnDestroy()
    {
        if (UnloadWatcher.isQuitting || UnloadWatcher.isUnloading)
            return;

        if (NetworkLifecycle.Instance.IsHost())
        {
            NetworkedItemManager.Instance.AddDirtyItemSnapshot(this, CreateUpdateData(ItemUpdateData.ItemUpdateType.Destroy));
        }
        /*
        else if(!BlockSync)
        {
            Multiplayer.LogWarning($"NetworkedItem.OnDestroy({name}, {NetId})");/*\r\n{new System.Diagnostics.StackTrace()}
        }
        else
        {
            Multiplayer.LogDebug(()=>$"NetworkedItem.OnDestroy({name}, {NetId})");/*\r\n{new System.Diagnostics.StackTrace()}
        }*/

        if (Item != null)
        {
            Item.Grabbed -= OnGrabbed;
            Item.Ungrabbed -= OnUngrabbed;
            //Item.ItemInventoryStateChanged -= OnItemInventoryStateChanged;
            itemBaseToNetworkedItem.Remove(Item);
        }
        else
        {
            Multiplayer.LogWarning($"NetworkedItem.OnDestroy({name}, {NetId}) Item is null!");
        }

        base.OnDestroy();

    }

    public string GetDirtyValuesDebugString()
    {
        var dirtyValues = trackedValues.Where(tv => ((dynamic)tv).IsDirty).ToList();
        if (dirtyValues.Count == 0)
        {
            return "No dirty values";
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"Dirty values for NetworkedItem {name}, NetId {NetId}:");
        foreach (var value in dirtyValues)
        {
            sb.AppendLine(((dynamic)value).GetDebugString());
        }
        return sb.ToString();
    }
}
