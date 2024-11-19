using HarmonyLib;
using Multiplayer.Components.Networking.World;
using Multiplayer.Utils;
using UnityEngine;

namespace Multiplayer.Patches.World.Items;

[HarmonyPatch(typeof(FlashlightItem))]
public static class FlashlightItemPatch
{
    [HarmonyPatch(nameof(FlashlightItem.Start))] 
    static void Postfix(FlashlightItem __instance)
    {
        var networkedItem = __instance.gameObject.GetOrAddComponent<NetworkedItem>();
        networkedItem.Initialize(__instance);

        // Register the values you want to track with both getters and setters
        networkedItem.RegisterTrackedValue(
            "originalLightIntensity ",
            () => __instance.originalLightIntensity,
            value => __instance.originalLightIntensity = value
            );

        networkedItem.RegisterTrackedValue(
            "originalLightIntensity ",
            () => __instance.originalLightIntensity,
            value => __instance.originalLightIntensity = value
            );

        networkedItem.RegisterTrackedValue(
            "intensity",
            () => __instance.originalLightIntensity,
            value =>  __instance.spotlight.intensity = value
            );

        networkedItem.RegisterTrackedValue(
            "originalBeamColour",
            () => __instance.originalBeamColor.ColorToUInt32(),
            value =>__instance.originalBeamColor = value.UInt32ToColor()
            );

        networkedItem.RegisterTrackedValue(
            "beamColour",
            () => __instance.beamController.GetBeamColor().ColorToUInt32(),
            value =>
            {
                Color colour = value.UInt32ToColor();
                __instance.beamController.SetBeamColor(colour);
                __instance.spotlight.color = colour;
            }
            );

        networkedItem.RegisterTrackedValue(
            "batteryCurrentPower",
            () => __instance.battery.currentPower,
            value =>
                {
                    __instance.battery.currentPower = value; //set the value
                    __instance.battery.UpdatePower(0f);      //process a delta of 0 to force an update
                }
            );

        networkedItem.RegisterTrackedValue(
            "buttonState",
            () => (__instance.button.Value > 0f),
            value =>
                {
                    if (value)
                        __instance.button.SetValue(1f);
                    else
                        __instance.button.SetValue(0f);

                    __instance.ToggleFlashlight(value);
                }
            );

        //This may not be required testing needed
        /*
        networkedItem.RegisterTrackedValue(
            "batteryDepleted",
            () => __instance.battery.Depleted,
            value =>__instance.battery.Depleted = value
            );
        */

        networkedItem.FinaliseTrackedValues();
    }
}
