using LiteNetLib.Utils;
using System;

namespace Multiplayer.Networking.Data;

public readonly struct TrainsetMovementPart
{
    public readonly TrainsetMovementType MovementType;
    public readonly float Speed;
    public readonly float SlowBuildUpStress;
    public readonly BogieData Bogie1;
    public readonly BogieData Bogie2;
    public readonly RigidbodySnapshot RigidbodySnapshot;

    [Flags]
    public enum TrainsetMovementType : byte
    {
        RigidBody = 1,
        Bogie = 2,
        All = byte.MaxValue
    }

    public TrainsetMovementPart(float speed, float slowBuildUpStress, BogieData bogie1, BogieData bogie2, RigidbodySnapshot rigidbodySnapshot = null)
    {
        MovementType = rigidbodySnapshot != null ? TrainsetMovementType.All : TrainsetMovementType.Bogie;
        Speed = speed;
        SlowBuildUpStress = slowBuildUpStress;
        Bogie1 = bogie1;
        Bogie2 = bogie2;
        RigidbodySnapshot = rigidbodySnapshot;
    }

    public TrainsetMovementPart(RigidbodySnapshot rigidbodySnapshot)
    {
        MovementType = TrainsetMovementType.RigidBody;
        RigidbodySnapshot = rigidbodySnapshot;
    }

#pragma warning disable EPS05
    public static void Serialize(NetDataWriter writer, TrainsetMovementPart data)
#pragma warning restore EPS05
    {
        writer.Put((byte)data.MovementType);

        if (data.MovementType.HasFlag(TrainsetMovementType.RigidBody))
            RigidbodySnapshot.Serialize(writer, data.RigidbodySnapshot);

        if (!data.MovementType.HasFlag(TrainsetMovementType.Bogie))
            return;

        writer.Put(data.Speed);
        writer.Put(data.SlowBuildUpStress);
        BogieData.Serialize(writer, data.Bogie1);
        BogieData.Serialize(writer, data.Bogie2);
    }

    public static TrainsetMovementPart Deserialize(NetDataReader reader)
    {
        TrainsetMovementType movementType = (TrainsetMovementType)reader.GetByte();
        RigidbodySnapshot snapshot = null;

        if (movementType.HasFlag(TrainsetMovementType.RigidBody))
            snapshot = RigidbodySnapshot.Deserialize(reader);

        if (movementType.HasFlag(TrainsetMovementType.Bogie))
        {
            float speed = reader.GetFloat();
            float slowBuildUpStress = reader.GetFloat();
            BogieData bogie1 = BogieData.Deserialize(reader);
            BogieData bogie2 = BogieData.Deserialize(reader);

            return new TrainsetMovementPart(
                reader.GetFloat(),
                reader.GetFloat(),
                BogieData.Deserialize(reader),
                BogieData.Deserialize(reader),
                snapshot
            );
        }

        return new TrainsetMovementPart(snapshot);
    }
}
