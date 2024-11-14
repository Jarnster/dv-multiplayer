using HarmonyLib;
using Multiplayer.Components.Networking.World;
using Multiplayer.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Multiplayer.Patches.World.Items;

[HarmonyPatch(typeof(Lighter), "Start")]
public static class LighterPatch
{
    static void Postfix(Lighter __instance)
    {
        var networkedItem = __instance.gameObject.GetOrAddComponent<NetworkedItem>();

        __instance.StartCoroutine(Init(networkedItem, __instance));
    }

    private static IEnumerator Init(NetworkedItem netItem, Lighter lighter)
    {
        while (!lighter.initialized)
            yield return null;

        netItem.Initialize(lighter);

        // Register the values you want to track with both getters and setters
        netItem.RegisterTrackedValue(
            "isOpen",
            () => lighter.isOpen,
            value =>
            {
                if (value)
                    lighter.OpenLid();
                else
                    lighter.CloseLid();
            }
            );

        netItem.RegisterTrackedValue(
            "Ignited",
            () => lighter.igniter.enabled,
            value =>
            {
                if (value)
                    lighter.LightFire(true, true);
                else
                    lighter.OnFlameExtinguished();
            }
            );
    }
}
