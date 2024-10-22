using System;
using System.Collections.Generic;
using Multiplayer.Components.Networking.Train;
using Multiplayer.Components.Networking.World;
using UnityEngine;

namespace Multiplayer.Networking.Data;

public class ServerPlayer
{
    public byte Id { get; set; }
    public bool IsLoaded { get; set; }
    public string Username { get; set; }
    public string OriginalUsername { get; set; }
    public Guid Guid { get; set; }
    public Vector3 RawPosition { get; set; }
    public float RawRotationY { get; set; }
    public ushort CarId { get; set; }

    public Dictionary<NetworkedItem, uint> KnownItems { get; private set; } = new Dictionary<NetworkedItem, uint>(); //NetworkedItem, last updated tick
    public Dictionary<NetworkedItem, float> NearbyItems { get; private set; } = new Dictionary<NetworkedItem, float>(); //NetworkedItem, time since near the item
    public StorageBase Storage { get; set; } = new StorageBase();

    private Vector3 _lastWorldPos = Vector3.zero;
    private Vector3 _lastAbsoluteWorldPosition = Vector3.zero;

    public Vector3 AbsoluteWorldPosition
    {
        get
        {

            Vector3 pos;
            try
            {
                if (CarId == 0 || !NetworkedTrainCar.Get(CarId, out NetworkedTrainCar car))
                {
                    if (CarId != 0)
                        Multiplayer.LogDebug(() => $"AbsoluteWorldPosition() noID {Username}: CarId: {CarId}");

                    pos = RawPosition;
                }
                else
                {
                    //Multiplayer.LogDebug(() => $"AbsoluteWorldPosition() hasID {Username}: CarId: {CarId}");
                    pos = car.transform.TransformPoint(RawPosition) - WorldMover.currentMove; ;
                }

                _lastAbsoluteWorldPosition = pos;
            }
            catch (Exception e)
            {
                Multiplayer.LogWarning($"AbsoluteWorldPosition() Exception {Username}");
                Multiplayer.LogWarning(e.Message);
                Multiplayer.LogWarning(e.StackTrace);
                pos = _lastAbsoluteWorldPosition;
            }

            return pos;

        }
    }

    public Vector3 WorldPosition {
        get
        {
            Vector3 pos;
            try
            {
                if (CarId == 0 || !NetworkedTrainCar.Get(CarId, out NetworkedTrainCar car))
                {
                    if(CarId != 0)
                        Multiplayer.LogDebug(() =>$"WorldPosition() noID {Username}: CarId: {CarId}");

                    pos = RawPosition + WorldMover.currentMove;
                }
                else
                {
                    //Multiplayer.LogDebug(() => $"WorldPosition() hasID {Username}: CarId: {CarId}");
                    pos = car.transform.TransformPoint(RawPosition);
                }

                _lastWorldPos = pos;
            }
            catch (Exception e)
            {
                Multiplayer.LogWarning($"WorldPosition() Exception {Username}");
                Multiplayer.LogWarning(e.Message);
                Multiplayer.LogWarning(e.StackTrace);

                pos = _lastWorldPos;
            }

            return pos;
        }
    }  
    public float WorldRotationY => CarId == 0 || !NetworkedTrainCar.Get(CarId, out NetworkedTrainCar car)
        ? RawRotationY
        : (Quaternion.Euler(0, RawRotationY, 0) * car.transform.rotation).eulerAngles.y;

    public override string ToString()
    {
        return $"{Id} ({Username}, {Guid.ToString()})";
    }
}
