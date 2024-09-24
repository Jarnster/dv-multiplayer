using HarmonyLib;
using Multiplayer.Components.Networking.World;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multiplayer.Patches.World.Items;

[HarmonyPatch(typeof(Lighter), "Awake")]
public static class LighterAwakePatch
{
    static void Postfix(Lighter __instance)
    {
        var networkedItem = __instance.gameObject.AddComponent<NetworkedItem>();
        networkedItem.Initialize(__instance);

        // Register the values you want to track with both getters and setters
        networkedItem.RegisterTrackedValue(
            "isOpen",
            () => __instance.isOpen,
            value =>
                    {
                        if (value)
                            __instance.OpenLid();
                        else
                            __instance.CloseLid();
                    }
            );

        networkedItem.RegisterTrackedValue(
            "Ignited",
            () => __instance.igniter.enabled,
            value =>
                    {
                        if (value)
                            __instance.LightFire(true, true);
                        else
                            __instance.OnFlameExtinguished();
                    }
            );
    }
}
