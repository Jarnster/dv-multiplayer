using DV.CabControls;
using DV.InventorySystem;
using DV.Simulation.Brake;
using Multiplayer.Components.Networking.Jobs;
using Multiplayer.Components.Networking.Train;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
    #endregion

    public ItemBase Item { get; set; }
    public Guid Owner { get; set; }

    protected override bool IsIdServerAuthoritative => true;

    protected override void Awake()
    {
        base.Awake();

        Multiplayer.LogDebug(()=>$"NetworkedItem.Awake() {name}");

        if (!TryGetComponent(out ItemBase item))
        {
            Multiplayer.LogError($"Unable to find ItemBase for {name}");
            return;
        }

        Item = item;
        itemBaseToNetworkedItem[Item] = this;
        SetupItem();
    }

    private void Start()
    {
        
    }

    private void SetupItem()
    {
        //Let's get the item type and take an appropriate action
        string itemType = Item?.InventorySpecs?.itemPrefabName;

        Multiplayer.LogDebug(() => $"NetworkedItem.SetupItem() {name}, {itemType}");

        switch (itemType)
        {
            //Job related items
            case "JobOverview":
                SetupJobOverview();
                break;

            case "JobBooklet":
                //SetupJobBooklet();
                break;

            case "JobMissingLicenseReport":
                SetupJobMissingLicenseReport();
                break;

            case "JobDebtWarningReport":
                SetupJobDebtWarningReport();
                break;

            //Loco related items
            case "lighter":
                break;

            case "Shovel":
                break;

            //Other interactables
            case "Lantern":
                break;

            //Non interactables
            default:
                break;
        }

        Item.Grabbed += OnGrabbed;
        Item.Ungrabbed += OnUngrabbed;
        Item.ItemInventoryStateChanged += OnItemInventoryStateChanged;
    }

    private void OnUngrabbed(ControlImplBase obj)
    {
        Multiplayer.LogDebug(() => $"OnUngrabbed() {name}");
    }

    private void OnGrabbed(ControlImplBase obj)
    {
        Multiplayer.LogDebug(() => $"OnGrabbed() {name}");
    }

    private void OnItemInventoryStateChanged(ItemBase itemBase, InventoryActionType actionType, InventoryItemState itemState)
    {
        Multiplayer.LogDebug(() => $"OnItemInventoryStateChanged() {name}, InventoryActionType: {actionType}, InventoryItemState: {itemState}");
    }

    private void SetupJobOverview()
    {
        if(!TryGetComponent(out JobOverview jobOverview))
        {
            Multiplayer.LogError($"SetupJobOverview() Could not find JobOverview");
            return;
        }

        if (!NetworkedJob.TryGetFromJob(jobOverview.job, out NetworkedJob networkedJob))
        {
            Multiplayer.LogError($"SetupJobOverview() NetworkedJob not found for Job ID: {jobOverview?.job?.ID}");
            jobOverview.DestroyJobOverview();
            return;
        }

        networkedJob.JobOverview = jobOverview;
        networkedJob.ValidationItem = this;
    }

    //private IEnumerator SetupJobBooklet()
    //{
    //    if (!TryGetComponent(out JobBooklet jobBooklet))
    //    {
    //        Multiplayer.LogError($"SetupJobBooklet() Could not find JobBooklet");
    //        yield break;
    //    }

    //    while (jobBooklet.job == null)
    //        yield return new WaitForEndOfFrame();

    //    if (!NetworkedJob.TryGetFromJob(jobBooklet.job, out NetworkedJob networkedJob))
    //    {
    //        Multiplayer.LogError($"SetupJobOverview() NetworkedJob not found for Job ID: {jobBooklet?.job?.ID}");
    //        jobBooklet.DestroyJobBooklet();
    //    }

    //    networkedJob.JobBooklet = jobBooklet;
    //    networkedJob.ValidationItem = this;
    //}

    private void SetupJobMissingLicenseReport()
    {
        if (!TryGetComponent(out JobMissingLicenseReport report))
        {
            Multiplayer.LogError($"SetupJobLicenseReport() Could not find JobMissingLicenseReport");
            return;
        }

        if (!NetworkedJob.TryGetFromJobId(report.jobId, out NetworkedJob networkedJob))
        {
            Multiplayer.LogError($"SetupJobLicenseReport() NetworkedJob not found for Job ID: {report?.jobId}");
            return;
        }

        networkedJob.ValidationItem = this;
    }
    private void SetupJobDebtWarningReport()
    {
        if (!TryGetComponent(out JobMissingLicenseReport report))
        {
            Multiplayer.LogError($"SetupJobDebtWarningReport() Could not find SetupJobDebtWarningReport");
            return;
        }

        if (!NetworkedJob.TryGetFromJobId(report.jobId, out NetworkedJob networkedJob))
        {
            Multiplayer.LogError($"SetupJobDebtWarningReport() NetworkedJob not found for Job ID: {report?.jobId}");
            return;
        }

        networkedJob.ValidationItem = this;
    }

}
