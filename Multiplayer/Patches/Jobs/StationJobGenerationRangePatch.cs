using HarmonyLib;
using Multiplayer.Components.Networking;
using Multiplayer.Networking.Data;
using UnityEngine;

namespace Multiplayer.Patches.Jobs;

[HarmonyPatch(typeof(StationJobGenerationRange), nameof(StationJobGenerationRange.PlayerSqrDistanceFromStationCenter), MethodType.Getter)]
public static class StationJobGenerationRange_PlayerSqrDistanceFromStationCenter_Patch
{
    private static int frameCount = 0;
    private static bool Prefix(StationJobGenerationRange __instance, ref float __result)
    {
        if (!NetworkLifecycle.Instance.IsHost())
            return true;

        Vector3 anchor = __instance.stationCenterAnchor.position;
        Vector3 anchor2 = anchor - WorldMover.currentMove;

        __result = float.MaxValue;

        //Loop through all of the players and return the one thats closest to the anchor
        foreach (ServerPlayer serverPlayer in NetworkLifecycle.Instance.Server.ServerPlayers)
        {
            float sqDist = (serverPlayer.WorldPosition - anchor).sqrMagnitude;
            //float sqDist2 = (serverPlayer.AbsoluteWorldPosition - anchor2).sqrMagnitude;
            float sqDist3 = (PlayerManager.PlayerTransform.position - __instance.stationCenterAnchor.position).sqrMagnitude;

            if (sqDist < __result)
                __result = sqDist;

            if (/*frameCount == 60 &&*/ Multiplayer.specLog && __instance.name == "StationFRS")
            {
                Multiplayer.LogDebug(() => $"PlayerSqrDistanceFromStationCenter:\r\n\t" +
                                            $"player: '{serverPlayer.Username}',\r\n\t\t" +
                                                //$"absPos: {serverPlayer.AbsoluteWorldPosition.ToString()},\r\n\t\t" +
                                                $"rawPos: {serverPlayer.RawPosition.ToString()},\r\n\t\t" +
                                                $"worldPos: {serverPlayer.WorldPosition.ToString()},\r\n\t" +
                                            $"station name: '{__instance.name}',\r\n\t\t" +
                                                $"anchor: {anchor.ToString()},\r\n\t\t" +
                                                $"anchor2: {anchor - WorldMover.currentMove},\r\n\t\t" +
                                                $"anchorTransform: {__instance.transform.TransformPoint(anchor)},\r\n\t\t" +
                                                $"anchorTransform2: {__instance.transform.TransformPoint(anchor) - WorldMover.currentMove},\r\n\t\t" +
                                                $"anchorInverseTransform: {__instance.transform.InverseTransformPoint(anchor)},\r\n\t\t" +
                                                $"anchorInverseTransform2: {__instance.transform.InverseTransformPoint(anchor) - WorldMover.currentMove},\r\n\t" +
                                            $"sqDist: {sqDist},\r\n\t" +
                                            //$"sqDist2: {sqDist2},\r\n\t" +
                                            $"sqDist3: {sqDist3},\r\n\t" +
                                            $"sqDistTransform: {(serverPlayer.WorldPosition - __instance.transform.TransformPoint(anchor)).sqrMagnitude},\r\n\t" +
                                            $"sqDistInverseTransform: {(serverPlayer.WorldPosition - __instance.transform.InverseTransformPoint(anchor)).sqrMagnitude}");
            }
        }

        frameCount++;
        if (frameCount > 60)
        {
            frameCount = 0;

        }

        return false;
    }
}

[HarmonyPatch(typeof(StationJobGenerationRange), nameof(StationJobGenerationRange.PlayerSqrDistanceFromStationOffice), MethodType.Getter)]
public static class StationJobGenerationRange_PlayerSqrDistanceFromStationOffice_Patch
{
    private static int frameCount = 0;
    private static bool Prefix(StationJobGenerationRange __instance, ref float __result)
    {
        if (!NetworkLifecycle.Instance.IsHost())
            return true;

        Vector3 anchor = __instance.transform.position;
        Vector3 anchor2 = anchor - WorldMover.currentMove;

        __result = float.MaxValue;
        //Loop through all of the players and return the one thats closest to the anchor
        foreach (ServerPlayer serverPlayer in NetworkLifecycle.Instance.Server.ServerPlayers)
        {
            float sqDist = (serverPlayer.WorldPosition - anchor).sqrMagnitude;
            //float sqDist2 = (serverPlayer.AbsoluteWorldPosition - anchor2).sqrMagnitude;
            float sqDist3 = (PlayerManager.PlayerTransform.position - __instance.stationCenterAnchor.position).sqrMagnitude;

            if (sqDist < __result)
                __result = sqDist;

            if (/*frameCount == 60 &&*/ Multiplayer.specLog &&  __instance.name == "StationFRS")
            {
                Multiplayer.LogDebug(() => $"PlayerSqrDistanceFromStationOffice:\r\n\t" +
                                            $"player: '{serverPlayer.Username}',\r\n\t\t" +
                                                //$"absPos: {serverPlayer.AbsoluteWorldPosition.ToString()},\r\n\t\t" +
                                                $"rawPos: {serverPlayer.RawPosition.ToString()},\r\n\t\t" +
                                                $"worldPos: {serverPlayer.WorldPosition.ToString()},\r\n\t" +
                                            $"station name: '{__instance.name}',\r\n\t\t" +
                                                $"anchor: {anchor.ToString()},\r\n\t\t" +
                                                $"anchor2: {anchor - WorldMover.currentMove},\r\n\t\t" +
                                                $"anchorTransform: {__instance.transform.TransformPoint(anchor)},\r\n\t\t" +
                                                $"anchorTransform2: {__instance.transform.TransformPoint(anchor) - WorldMover.currentMove},\r\n\t\t" +
                                                $"anchorInverseTransform: {__instance.transform.InverseTransformPoint(anchor)},\r\n\t\t" +
                                                $"anchorInverseTransform2: {__instance.transform.InverseTransformPoint(anchor) - WorldMover.currentMove},\r\n\t" +
                                            $"sqDist: {sqDist},\r\n\t" +
                                            //$"sqDist2: {sqDist2},\r\n\t" +
                                            $"sqDist3: {sqDist3},\r\n\t" +
                                            $"sqDistTransform: {(serverPlayer.WorldPosition - __instance.transform.TransformPoint(anchor)).sqrMagnitude},\r\n\t" +
                                            $"sqDistInverseTransform: {(serverPlayer.WorldPosition - __instance.transform.InverseTransformPoint(anchor)).sqrMagnitude}");
            }
        }

        frameCount++;
        if (frameCount > 60)
        {
            frameCount = 0;

        }
        return false;
    }
}
