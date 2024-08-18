using LiteNetLib.Utils;
using Multiplayer.Networking.Serialization;
using System;
using UnityEngine;
namespace Multiplayer.Networking.Data;

public readonly struct TrainsetMovementPart
{
    public readonly MovementType typeFlag;
    public readonly float Speed;
    public readonly float SlowBuildUpStress;
    public readonly Vector3 Position;       //Used in sync only
    public readonly Quaternion Rotation;    //Used in sync only
    public readonly BogieData Bogie1;
    public readonly BogieData Bogie2;
    public readonly RigidbodySnapshot RigidbodySnapshot;

    [Flags]
    public enum MovementType : byte
    {
        Physics = 1,
        RigidBody = 2,
        Sync = 4
    }

    public TrainsetMovementPart(float speed, float slowBuildUpStress, BogieData bogie1, BogieData bogie2, Vector3? position = null, Quaternion? rotation = null)
    {
        typeFlag = MovementType.Physics;    //no rigid body data

        Speed = speed;
        SlowBuildUpStress = slowBuildUpStress;
        Bogie1 = bogie1;
        Bogie2 = bogie2;

        if(position != null && rotation != null)
        {
            //Multiplayer.LogDebug(()=>$"new TrainsetMovementPart() Sync");

            typeFlag |= MovementType.Sync;  //includes positional data

            Position = (Vector3)position;
            Rotation = (Quaternion)rotation;
        }
    }

    public TrainsetMovementPart(RigidbodySnapshot rigidbodySnapshot)
    {
        typeFlag = MovementType.RigidBody;    //rigid body data

        //Multiplayer.LogDebug(() => $"new TrainsetMovementPart() RigidBody");

        RigidbodySnapshot = rigidbodySnapshot;
    }

#pragma warning disable EPS05
    public static void Serialize(NetDataWriter writer, TrainsetMovementPart data)
#pragma warning restore EPS05
    {
        writer.Put((byte)data.typeFlag);

        //Multiplayer.LogDebug(() => $"TrainsetMovementPart.Serialize() {data.typeFlag}");

        if (data.typeFlag == MovementType.RigidBody)
        {
            RigidbodySnapshot.Serialize(writer, data.RigidbodySnapshot);
            return;
        }

        writer.Put(data.Speed);
        writer.Put(data.SlowBuildUpStress);
        BogieData.Serialize(writer, data.Bogie1);
        BogieData.Serialize(writer, data.Bogie2);

        if (data.typeFlag.HasFlag(MovementType.Sync))   //serialise positional data
        {
            Vector3Serializer.Serialize(writer, data.Position);
            QuaternionSerializer.Serialize(writer, data.Rotation);
        }
    }

    public static TrainsetMovementPart Deserialize(NetDataReader reader)
    {
        MovementType dataType = (MovementType)reader.GetByte();

        //Multiplayer.LogDebug(() => $"TrainsetMovementPart.Deserialize() {dataType}");

        if (dataType == MovementType.RigidBody)
        {
            return new TrainsetMovementPart(RigidbodySnapshot.Deserialize(reader));
        }
        else
        {
            float speed = reader.GetFloat();
            float slowBuildUpStress = reader.GetFloat();
            BogieData bd1 = BogieData.Deserialize(reader);
            BogieData bd2 = BogieData.Deserialize(reader);

            Vector3? position = null;
            Quaternion? rotation = null;

            if (dataType.HasFlag(MovementType.Sync))
            {
                position = Vector3Serializer.Deserialize(reader);
                rotation = QuaternionSerializer.Deserialize(reader);
            }

            return new TrainsetMovementPart(speed,  slowBuildUpStress, bd1, bd2, position, rotation);
        }
    }
}
