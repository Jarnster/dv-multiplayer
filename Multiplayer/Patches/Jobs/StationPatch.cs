using DV.Logic.Job;
using HarmonyLib;
using Multiplayer.Components;
using Multiplayer.Components.Networking;
using Multiplayer.Components.Networking.Jobs;
using Multiplayer.Components.Networking.Train;
using Multiplayer.Components.Networking.World;
using Multiplayer.Utils;

namespace Multiplayer.Patches.Jobs;

[HarmonyPatch(typeof(Station), nameof(Station.AddJobToStation))]
public static class Station_AddJobToStation_Patch
{
    private static bool Prefix(Station __instance, Job job)
    {
        if (!NetworkLifecycle.Instance.IsHost())
            return false;

        Multiplayer.Log($"Station.AddJobToStation() adding NetworkJob for stationId: {__instance.ID}, jobId: {job.ID}");
        
        if(!NetworkedStationController.GetFromStationId(__instance.ID, out NetworkedStationController netStationController))
            return false;
        
        netStationController.AddJob(job);

        return true;
    }
}
