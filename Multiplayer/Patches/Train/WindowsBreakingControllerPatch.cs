using DV.Damage;
using HarmonyLib;
using Multiplayer.Components.Networking;
using Multiplayer.Utils;
using UnityEngine;

namespace Multiplayer.Patches.World;

[HarmonyPatch(typeof(WindowsBreakingController))]
public static class WindowsBreakingController_BreakWindowsFromCollision_Patch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(WindowsBreakingController.BreakWindowsFromCollision))]
    public static void BreakWindowsFromCollision_Postfix(WindowsBreakingController __instance, Vector3 forceDirection)
    {
        if (!NetworkLifecycle.Instance.IsHost())
            return;

        TrainCar car = TrainCar.Resolve(__instance.transform);
        if (car == null)
        {
            Multiplayer.LogWarning($"BreakWindowsFromCollision failed, unable to resolve TrainCar");
            return;
        }

        ushort netId = car.GetNetId();
        if(netId == 0)
        {
            Multiplayer.LogWarning($"BreakWindowsFromCollision failed, {car.name}");
            return; 
        }

        NetworkLifecycle.Instance.Server.SendWindowsBroken(netId, forceDirection);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(WindowsBreakingController.RepairWindows))]
    public static void RepairWindows_Postfix(WindowsBreakingController __instance)
    {
        if (!NetworkLifecycle.Instance.IsHost())
            return;
        ushort netId = TrainCar.Resolve(__instance.transform).GetNetId();
        NetworkLifecycle.Instance.Server.SendWindowsRepaired(netId);
    }
}
