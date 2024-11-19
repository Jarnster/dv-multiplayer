using HarmonyLib;
using Multiplayer.Components.Networking.World;
using Multiplayer.Utils;

namespace Multiplayer.Patches.World.Items;

[HarmonyPatch(typeof(Lantern), "Awake")]
public static class LanternAwakePatch
{
    static void Postfix(Lantern __instance)
    {
        var networkedItem = __instance.gameObject.GetOrAddComponent<NetworkedItem>();
        networkedItem.Initialize(__instance);

        // Register the values you want to track with both getters and setters
        networkedItem.RegisterTrackedValue(
            "wickSize",
            () => __instance.wickSize,
            value => {
                        __instance.UpdateWickRelatedLogic(value);
                     }
            );

        networkedItem.RegisterTrackedValue(
            "Ignited",
            () => __instance.igniter.enabled,
            value =>
                    {
                        if (value)
                            __instance.Ignite(1);
                        else
                            __instance.OnFlameExtinguished();
                    }
            );

        networkedItem.FinaliseTrackedValues();
    }
}
