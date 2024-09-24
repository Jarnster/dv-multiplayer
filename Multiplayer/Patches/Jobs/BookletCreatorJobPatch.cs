using DV.Booklets;
using DV.Logic.Job;
using HarmonyLib;
using Multiplayer.Components.Networking;
using Multiplayer.Components.Networking.Jobs;
using Multiplayer.Components.Networking.World;
using Multiplayer.Utils;
using UnityEngine;


namespace Multiplayer.Patches.Jobs;

[HarmonyPatch(typeof(BookletCreator))]
public static class BookletCreatorJob_Patch
{
    [HarmonyPatch(nameof(BookletCreator.CreateJobOverview))]
    [HarmonyPostfix]
    private static void CreateJobOverview(JobOverview __result, Job job)
    {
        if (!NetworkLifecycle.Instance.IsHost())
            return;

        if (!NetworkedJob.TryGetFromJob(job, out NetworkedJob networkedJob))
        {
            Multiplayer.LogError($"BookletCreatorJob_Patch.CreateJobOverview() NetworkedJob not found for Job ID: {job.ID}");
        }
        else
        {
            NetworkedItem netItem = __result.GetOrAddComponent<NetworkedItem>();
            netItem.Initialize(__result, 0, false);
            networkedJob.JobOverview =  netItem;
        }
    }

    [HarmonyPatch(nameof(BookletCreator.CreateJobBooklet))]
    [HarmonyPostfix]
    private static void CreateJobBooklet(JobBooklet __result, Job job)
    {
        if (!NetworkLifecycle.Instance.IsHost())
            return;

        if (!NetworkedJob.TryGetFromJob(job, out NetworkedJob networkedJob))
        {
            Multiplayer.LogError($"BookletCreatorJob_Patch.CreateJobBooklet() NetworkedJob not found for Job ID: {job.ID}");
        }
        else
        {
            NetworkedItem netItem = __result.GetOrAddComponent<NetworkedItem>();
            netItem.Initialize(__result, 0, false);
            networkedJob.JobBooklet = netItem;
        }
    }
}
