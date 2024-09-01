using HarmonyLib;
using Multiplayer.Components.Networking.Jobs;


namespace Multiplayer.Patches.Jobs;

[HarmonyPatch(typeof(JobBooklet))]
public static class JobBooklet_Patch
{
    [HarmonyPatch(nameof(JobBooklet.Awake))]
    [HarmonyPostfix]
    private static void Awake(JobBooklet __instance)
    {
        if(!NetworkedJob.TryGetFromJob(__instance.job, out NetworkedJob networkedJob))
        {
            Multiplayer.LogError($"JobBooklet.Awake() NetworkedJob not found for Job ID: {__instance.job?.ID}");
            return;
        }

        networkedJob.JobBooklet = __instance;
    }


    [HarmonyPatch(nameof(JobBooklet.DestroyJobBooklet))]
    [HarmonyPrefix]
    private static void DestroyJobBooklet(JobBooklet __instance)
    {
        if (!NetworkedJob.TryGetFromJob(__instance.job, out NetworkedJob networkedJob))
            Multiplayer.LogError($"JobBooklet.DestroyJobBooklet() NetworkedJob not found for Job ID: {__instance.job?.ID}");
        else
            networkedJob.JobBooklet = null;
    }
}
