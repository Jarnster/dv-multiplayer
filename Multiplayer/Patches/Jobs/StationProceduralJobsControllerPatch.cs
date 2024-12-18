using HarmonyLib;
using Multiplayer.Components.Networking;

namespace Multiplayer.Patches.Jobs;

[HarmonyPatch(typeof(StationProceduralJobsController), nameof(StationProceduralJobsController.TryToGenerateJobs))]
public static class StationProceduralJobsController_TryToGenerateJobs_Patch
{
    private static bool Prefix()
    {
        return NetworkLifecycle.Instance.IsHost();
    }
}
