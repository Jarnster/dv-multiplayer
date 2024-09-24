using DV.CabControls;
using DV.CabControls.Spec;
using DV.InventorySystem;
using Multiplayer.Components.Networking.Train;
using Multiplayer.Networking.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Multiplayer.Components.Networking.World;

public class NetworkedItem : IdMonoBehaviour<ushort, NetworkedItem>
{
    #region Lookup Cache
    private static readonly Dictionary<ItemBase, NetworkedItem> itemBaseToNetworkedItem = new();

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

    private const float PositionThreshold = 0.01f;
    private const float RotationThreshold = 0.1f;

    public ItemBase Item { get; private set; }
    private Component trackedItem;
    private List<object> trackedValues = new List<object>();
    public bool UsefulItem { get; private set; } = false;
    public Type TrackedItemType { get; private set; }

    //Track dirty states
    private bool CreatedDirty = true;   //if set, we created this item dirty and have not sent an update

    private bool ItemGrabbed = false;   //Current state of item grabbed
    private bool GrabbedDirty = false;  //Current state is dirty

    private bool ItemDropped = false;   //Current state of item dropped
    private bool DroppedDirty = false;  //Current state is dirty

    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private ItemPositionData ItemPosition;
    private bool PositionDirty = false;


    protected override bool IsIdServerAuthoritative => true;

    protected override void Awake()
    {
        base.Awake();
        Multiplayer.LogDebug(() => $"NetworkedItem.Awake() {name}");

        Register();
    }

    protected void Start()
    {
        if (!CreatedDirty)
            return;
    
        if (StorageController.Instance.IsInStorageWorld(Item))
        {
            ItemDropped = true;
        }
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
        if (!TryGetComponent(out ItemBase itemBase))
        {
            Multiplayer.LogError($"Unable to find ItemBase for {name}");
            return false;
        }

        Item = itemBase;
        itemBaseToNetworkedItem[Item] = this;

        Item.Grabbed += OnGrabbed;
        Item.Ungrabbed += OnUngrabbed;
        Item.ItemInventoryStateChanged += OnItemInventoryStateChanged;

        lastPosition = Item.transform.position - WorldMover.currentMove;
        lastRotation = Item.transform.rotation;

        return true;
    }

    private void OnUngrabbed(ControlImplBase obj)
    {
        Multiplayer.LogDebug(() => $"OnUngrabbed() {name}");
        GrabbedDirty = ItemGrabbed;
        ItemGrabbed = false;
        
    }

    private void OnGrabbed(ControlImplBase obj)
    {
        Multiplayer.LogDebug(() => $"OnGrabbed() {name}");
        GrabbedDirty = !ItemGrabbed;
        ItemGrabbed = true;
    }

    private void OnItemInventoryStateChanged(ItemBase itemBase, InventoryActionType actionType, InventoryItemState itemState)
    {
        Multiplayer.LogDebug(() => $"OnItemInventoryStateChanged() {name}, InventoryActionType: {actionType}, InventoryItemState: {itemState}");
        if (actionType == InventoryActionType.Purge)
        {
            DroppedDirty = !ItemDropped;
            ItemDropped = true;
        }
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

    private void CheckPositionChange()
    {
        Vector3 currentPosition = transform.position - WorldMover.currentMove;
        Quaternion currentRotation = transform.rotation;

        bool positionChanged = Vector3.Distance(currentPosition, lastPosition) > PositionThreshold;
        bool rotationChanged = Quaternion.Angle(currentRotation, lastRotation) > RotationThreshold;

        if (positionChanged || rotationChanged)
        {
            ItemPosition = new ItemPositionData
            {
                Position = currentPosition,
                Rotation = currentRotation
            };
            lastPosition = currentPosition;
            lastRotation = currentRotation;
            PositionDirty = true;
        }
    }

    private void Update()
    {
        ItemUpdateData snapshot;
        ItemUpdateData.ItemUpdateType updateType = ItemUpdateData.ItemUpdateType.None;

        if (Item == null && Register() ==false)
            return;

        CheckPositionChange();

        if (!CreatedDirty)
        {
            if(PositionDirty)
                updateType |= ItemUpdateData.ItemUpdateType.Position;
            if(DroppedDirty)
                updateType |= ItemUpdateData.ItemUpdateType.ItemDropped;
            if(GrabbedDirty)
                updateType |= ItemUpdateData.ItemUpdateType.ItemEquipped;
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

        snapshot = CreateUpdateData(updateType);
        NetworkedItemManager.Instance.AddDirtyItemSnapshot(snapshot);

        CreatedDirty = false;
        GrabbedDirty = false;
        DroppedDirty = false;
        PositionDirty = false;

        MarkValuesClean();
    }

    /*
    private void SendStateUpdate()
    {
        var updateData = CreateUpdateData(ItemUpdateData.ItemUpdateType.State);
        updateData.StateData = GetDirtyStateData();
        SendItemUpdate(updateData);
        MarkValuesClean();
    }
    */
    #endregion

    public ItemUpdateData CreateUpdateData(ItemUpdateData.ItemUpdateType updateType)
    {
   
        var updateData = new ItemUpdateData
        {
            UpdateType = updateType,
            ItemNetId = NetId,
            PrefabName = Item.name,
            PositionData = ItemPosition,
            Equipped = ItemGrabbed,
            Dropped = ItemDropped,
            States = GetDirtyStateData(),
        };

        return updateData;
    }


    protected override void OnDestroy()
    {
        if (UnloadWatcher.isQuitting || UnloadWatcher.isUnloading)
            return;

        if (NetworkLifecycle.Instance.IsHost())
        {
            NetworkedItemManager.Instance.AddDirtyItemSnapshot(CreateUpdateData(ItemUpdateData.ItemUpdateType.Destroy));
        }
        else
        {
            Multiplayer.LogWarning($"NetworkedItem.OnDestroy({name}, {NetId})\r\n{new System.Diagnostics.StackTrace()}");
        }

        base.OnDestroy();
        if (Item != null)
        {
            Item.Grabbed -= OnGrabbed;
            Item.Ungrabbed -= OnUngrabbed;
            Item.ItemInventoryStateChanged -= OnItemInventoryStateChanged;
            itemBaseToNetworkedItem.Remove(Item);
        }

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
