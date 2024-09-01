using System;
using System.Collections;
using System.Linq;
using DV;
using DV.ThingTypes;
using HarmonyLib;
using Multiplayer.Components.Networking;
using Multiplayer.Components.Networking.Jobs;
using Multiplayer.Components.Networking.World;
using Multiplayer.Networking.Packets.Clientbound.Jobs;

using UnityEngine;

namespace Multiplayer.Patches.Jobs;

[HarmonyPatch(typeof(JobValidator))]
public static class JobValidator_Patch
{
    [HarmonyPatch(nameof(JobValidator.Start))]
    [HarmonyPostfix]
    private static void Start_Postfix(JobValidator __instance)
    {

        string stationName = __instance.transform.parent.name ?? "";

        if (string.IsNullOrEmpty(stationName))
        {
            Multiplayer.LogError($"JobValidator.Start() Can not find parent's name");
            return;
        }

        stationName += "_office_anchor";

        StationController[] stations = StationController.allStations.Where(s => s.transform.parent.name.Equals(stationName,StringComparison.OrdinalIgnoreCase)).ToArray();

        if (stations.Length == 1)
        {
            if(!NetworkedStationController.GetFromStationController(stations.First(), out NetworkedStationController networkedStationController))
                Multiplayer.LogError($"JobValidator.Start() Could not find NetworkedStation for validator: {stationName}");
            else
                NetworkedStationController.RegisterJobValidator(__instance, networkedStationController);
        }
        else
        {
            Multiplayer.LogError($"JobValidator.Start() Found {stations.Length} stations for {stationName}");
        }
    }

    [HarmonyPatch(nameof(JobValidator.ProcessJobOverview))]
    [HarmonyPrefix]
    private static bool ProcessJobOverview_Prefix(JobValidator __instance, JobOverview jobOverview)
    {
        if (NetworkLifecycle.Instance.IsHost())
            return true;

        if(__instance.bookletPrinter.IsOnCooldown)
        {
            __instance.bookletPrinter.PlayErrorSound();
            return false;
        }

        if(!NetworkedJob.TryGetFromJob(jobOverview.job, out NetworkedJob networkedJob) || jobOverview.job.State != JobState.Available)
        {
            NetworkLifecycle.Instance.Client.LogWarning($"ProcessJobOverview_Prefix({jobOverview?.job?.ID}) NetworkedJob found: {networkedJob != null}, Job state: {jobOverview?.job?.State}");
            __instance.bookletPrinter.PlayErrorSound();
            jobOverview.DestroyJobOverview();
            return false;
        }

        if(networkedJob.ValidatorRequestSent)
        {
            if (networkedJob.ValidatorResponseReceived && networkedJob.ValidationAccepted)
                return true;
        }
        else
        {
            if(NetworkedStationController.GetFromJobValidator(__instance, out NetworkedStationController networkedStation))
            {
                //Set initial job state parameters
                networkedJob.ValidatorRequestSent = true;
                networkedJob.ValidatorResponseReceived = false;
                networkedJob.ValidationAccepted = false;

                NetworkLifecycle.Instance.Client.SendJobValidateRequest(networkedJob.NetId, networkedStation.NetId, ValidationType.JobOverview);
                CoroutineManager.Instance.StartCoroutine(AwaitResponse(__instance, networkedJob, ValidationType.JobOverview));
            }
            else
            {
                NetworkLifecycle.Instance.Client.LogError($"ProcessJobOverview_Prefix({jobOverview?.job?.ID}) Failed to find NetworkedStation");
                __instance.bookletPrinter.PlayErrorSound();
            }
        }

        return false;
    }

    [HarmonyPatch(nameof(JobValidator.ValidateJob))]
    [HarmonyPrefix]
    private static bool ValidateJob_Prefix(JobValidator __instance, JobBooklet jobBooklet)
    {
        if (NetworkLifecycle.Instance.IsHost())
            return true;

        if (__instance.bookletPrinter.IsOnCooldown)
        {
            __instance.bookletPrinter.PlayErrorSound();
            return false;
        }

        if (!NetworkedJob.TryGetFromJob(jobBooklet.job, out NetworkedJob networkedJob) || jobBooklet.job.State != JobState.InProgress)
        {
            NetworkLifecycle.Instance.Client.LogWarning($"ValidateJob({jobBooklet?.job?.ID}) NetworkedJob found: {networkedJob != null}, Job state: {jobBooklet?.job?.State}");
            __instance.bookletPrinter.PlayErrorSound();
            jobBooklet.DestroyJobBooklet();
            return false;
        }

        if (networkedJob.ValidatorRequestSent)
        {
            if (networkedJob.ValidatorResponseReceived && networkedJob.ValidationAccepted)
                return true;
        }
        else
        {
            //find the current station we're at
            if (NetworkedStationController.GetFromJobValidator(__instance, out NetworkedStationController networkedStation))
            {
                //Set initial job state parameters
                networkedJob.ValidatorRequestSent = true;
                networkedJob.ValidatorResponseReceived = false;
                networkedJob.ValidationAccepted = false;

                NetworkLifecycle.Instance.Client.SendJobValidateRequest(networkedJob.NetId, networkedStation.NetId, ValidationType.JobBooklet);
                CoroutineManager.Instance.StartCoroutine(AwaitResponse(__instance, networkedJob, ValidationType.JobBooklet));
            }
            else
            {
                NetworkLifecycle.Instance.Client.LogError($"ValidateJob({jobBooklet?.job?.ID}) Failed to find NetworkedStation");
                __instance.bookletPrinter.PlayErrorSound();
            }
        }

        return false;
    }

    private static IEnumerator AwaitResponse(JobValidator validator, NetworkedJob networkedJob, ValidationType type)
    {
        yield return new WaitForSecondsRealtime(NetworkLifecycle.Instance.Client.Ping * 2);

        NetworkLifecycle.Instance.Client.Log($"JobValidator_Patch.AwaitResponse() ResponseReceived: {networkedJob?.ValidatorResponseReceived}, Accepted: {networkedJob?.ValidationAccepted}");

        if (networkedJob == null || (!networkedJob.ValidatorResponseReceived || !networkedJob.ValidationAccepted))
        {
            validator.bookletPrinter.PlayErrorSound();
            yield break;
        }

        switch (type)
        {
            case ValidationType.JobOverview:
                validator.ProcessJobOverview(networkedJob.JobOverview);
                break;

            case ValidationType.JobBooklet:
                validator.ValidateJob(networkedJob.JobBooklet);
                break;
        }


    }
}
