using HarmonyLib;
using Multiplayer.Components.Networking.World;
using Multiplayer.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                            __instance.OnFlameIgnited();
                        else
                            __instance.OnFlameExtinguished();
                    }
            );
    }
}
