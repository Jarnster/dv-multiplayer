using LiteNetLib.Utils;
using Multiplayer.Components.Networking.World;
using System;
using System.Collections.Generic;

namespace Multiplayer.Networking.Data;

public class ItemUpdateData
{
    [Flags]
    public enum ItemUpdateType : byte
    {
        None = 0,
        Create = 1,
        Destroy = 2,
        Position = 4,
        ItemDropped = 8,
        ItemEquipped = 16,
        ObjectState = 32,
    }

    public ItemUpdateType UpdateType { get; set; }
    public ushort ItemNetId { get; set; }
    public string PrefabName { get; set; }
    public ItemPositionData PositionData { get; set; }
    
    public bool Dropped { get; set; }
    public bool Equipped { get; set; }
    public ushort Player { get; set; }
    public Dictionary<string, object> States { get; set; }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put((byte)UpdateType);
        writer.Put(ItemNetId);

        if(UpdateType == ItemUpdateType.Destroy)
            return;

        if (UpdateType.HasFlag(ItemUpdateType.Create))
            writer.Put(PrefabName);

        if (UpdateType.HasFlag(ItemUpdateType.Position) || UpdateType.HasFlag(ItemUpdateType.ItemDropped) || UpdateType.HasFlag(ItemUpdateType.Create))
            ItemPositionData.Serialize(writer, PositionData);

        if (UpdateType.HasFlag(ItemUpdateType.ItemDropped) || UpdateType.HasFlag(ItemUpdateType.Create))
            writer.Put(Dropped);

        if (UpdateType.HasFlag(ItemUpdateType.ItemEquipped) || UpdateType.HasFlag(ItemUpdateType.Create))
            writer.Put(Equipped);

        if (UpdateType.HasFlag(ItemUpdateType.ItemDropped) || UpdateType.HasFlag(ItemUpdateType.ItemEquipped) || UpdateType.HasFlag(ItemUpdateType.Create))
            writer.Put(Player);

        if (UpdateType.HasFlag(ItemUpdateType.ObjectState) || UpdateType.HasFlag(ItemUpdateType.Create))
        {
            if (States == null)
                writer.Put(0);
            else
            {
                writer.Put(States.Count);
                foreach(var state in States)
                {
                    writer.Put(state.Key);
                    SerializeTrackedValue(writer, state.Value);
                }
            }
        }
    }

    public void Deserialize(NetDataReader reader)
    {
        UpdateType = (ItemUpdateType)reader.GetByte();
        ItemNetId = reader.GetUShort();

        if (UpdateType == ItemUpdateType.Destroy)
            return;

        if (UpdateType == ItemUpdateType.Create)
            PrefabName = reader.GetString();

        if (UpdateType.HasFlag(ItemUpdateType.Position) || UpdateType.HasFlag(ItemUpdateType.ItemDropped) || UpdateType.HasFlag(ItemUpdateType.Create))
        {
            PositionData = ItemPositionData.Deserialize(reader);
        }

        if (UpdateType.HasFlag(ItemUpdateType.ItemDropped) || UpdateType.HasFlag(ItemUpdateType.Create))
            Dropped = reader.GetBool();

        if (UpdateType.HasFlag(ItemUpdateType.ItemEquipped) || UpdateType.HasFlag(ItemUpdateType.Create))
            Equipped = reader.GetBool();

        if (UpdateType.HasFlag(ItemUpdateType.ItemDropped) || UpdateType.HasFlag(ItemUpdateType.ItemEquipped) || UpdateType.HasFlag(ItemUpdateType.Create))
            Player = reader.GetUShort();

        if (UpdateType.HasFlag(ItemUpdateType.ObjectState) || UpdateType.HasFlag(ItemUpdateType.Create))
        {
            States = new Dictionary<string, object>();

            int stateCount = reader.GetInt();
            for (int i = 0; i < stateCount; i++)
            {
                string key = reader.GetString();
                object value = DeserializeTrackedValue(reader);
                States[key] = value;
            }
        }
    }

    private void SerializeTrackedValue(NetDataWriter writer, object value)
    {
        if (value is bool boolValue)
        {
            writer.Put((byte)0);
            writer.Put(boolValue);
        }
        else if (value is int intValue)
        {
            writer.Put((byte)1);
            writer.Put(intValue);
        }
        else if (value is float floatValue)
        {
            writer.Put((byte)2);
            writer.Put(floatValue);
        }
        else if (value is string stringValue)
        {
            writer.Put((byte)3);
            writer.Put(stringValue);
        }
        else
        {
            throw new NotSupportedException($"ItemUpdateData.SerializeTrackedValue({ItemNetId}, {PrefabName??""}) Unsupported type for serialization: {value.GetType()}");
        }
    }

    private object DeserializeTrackedValue(NetDataReader reader)
    {
        byte typeCode = reader.GetByte();
        switch (typeCode)
        {
            case 0: return reader.GetBool();
            case 1: return reader.GetInt();
            case 2: return reader.GetFloat();
            case 3: return reader.GetString();

            default:
                throw new NotSupportedException($"ItemUpdateData.DeserializeTrackedValue({ItemNetId}, {PrefabName ?? ""}) Unsupported type code for deserialization: {typeCode}");
        }
    }
}
