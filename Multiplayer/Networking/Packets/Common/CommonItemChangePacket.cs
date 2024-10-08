using LiteNetLib.Utils;
using Multiplayer.Networking.Data;
using System.Collections.Generic;
using System;

namespace Multiplayer.Networking.Packets.Common;

public class CommonItemChangePacket : INetSerializable
{
    private const int COMPRESS_AFTER_COUNT = 50;

    public List<ItemUpdateData> Items = new List<ItemUpdateData>();

    public void Deserialize(NetDataReader reader)
    {

        Items.Clear();

        Multiplayer.Log("CommonItemChangePacket.Deserialize()");

        //Multiplayer.LogDebug(() => $"CommonItemChangePacket.Deserialize()\r\nBytes: {BitConverter.ToString(reader.RawData).Replace("-", " ")}");
        Multiplayer.Log($"CommonItemChangePacket.Deserialize() Pre-itemCount {Items?.Count} ");
        try
        {
            bool compressed = reader.GetBool();
            if (compressed)
            {
                DeserializeCompressed(reader);
            }
            else
            {
                DeserializeRaw(reader);
            }

            Multiplayer.Log($"CommonItemChangePacket.Deserialize() post-itemCount {Items?.Count} ");
        }
        catch (Exception ex)
        {
            Multiplayer.LogError($"Error in CommonItemChangePacket.Deserialize: {ex.Message}");
        }
    }

    private void DeserializeCompressed(NetDataReader reader)
    {
        int itemCount = reader.GetInt();
        byte[] compressedData = reader.GetBytesWithLength();
        Multiplayer.Log($"CommonItemChangePacket.DeserializeCompressed() itemCount {itemCount} length: {compressedData.Length}");

        byte[] decompressedData = PacketCompression.Decompress(compressedData);
        //Multiplayer.Log($"CommonItemChangePacket.DeserializeCompressed() Compressed: {compressedData.Length} Decompressed: {decompressedData.Length}");

        NetDataReader decompressedReader = new NetDataReader(decompressedData);
        
        //Items.Capacity = itemCount;

        for (int i = 0; i < itemCount; i++)
        {
            var item = new ItemUpdateData();
            item.Deserialize(decompressedReader);
            Items.Add(item);
        }
    }

    private void DeserializeRaw(NetDataReader reader)
    {
        int itemCount = reader.GetInt();
        Multiplayer.Log($"CommonItemChangePacket.DeserializeRaw() itemCount: {itemCount}");

        //Items.Capacity = itemCount;

        //Multiplayer.Log($"CommonItemChangePacket.DeserializeRaw() itemCount: {itemCount}, pre-loop");
        for (int i = 0; i < itemCount; i++)
        {
            //Multiplayer.Log($"CommonItemChangePacket.DeserializeRaw() itemCount: {itemCount}, new ItemUpdateData()");
            var item = new ItemUpdateData();
            //Multiplayer.Log($"CommonItemChangePacket.DeserializeRaw() itemCount: {itemCount}, item.Deserialize()");
            item.Deserialize(reader);
            //Multiplayer.Log($"CommonItemChangePacket.DeserializeRaw() itemCount: {itemCount}, Items.Add()");
            Items.Add(item);
        }
    }

    public void Serialize(NetDataWriter writer)
    {
        Multiplayer.Log("CommonItemChangePacket.Serialize()");
        //Multiplayer.LogDebug(() => $"CommonItemChangePacket.Serialize() Data Before\r\nBytes: {BitConverter.ToString(writer.CopyData()).Replace("-", " ")}");
        
        try
        {
            if (Items.Count > COMPRESS_AFTER_COUNT)
            {
                SerializeCompressed(writer);
            }
            else
            {
                SerializeRaw(writer);
            }

            //Multiplayer.LogDebug(() => $"CommonItemChangePacket.Serialize() Data After\r\nBytes: {BitConverter.ToString(writer.CopyData()).Replace("-", " ")}");
        }
        catch (Exception ex)
        {
            Multiplayer.LogError($"CommonItemChangePacket.Serialize: {ex.Message}\r\n{ex.StackTrace}");
        }
    }

    private void SerializeCompressed(NetDataWriter writer)
    {
        Multiplayer.Log($"CommonItemChangePacket.Serialize() Compressing. Item Count: {Items.Count}");
        writer.Put(true); // compressed data stream
        writer.Put(Items.Count);

        NetDataWriter dataWriter = new NetDataWriter();

        foreach (var item in Items)
        {
            item.Serialize(dataWriter);
        }

        byte[] compressedData = PacketCompression.Compress(dataWriter.Data);
        Multiplayer.Log($"Uncompressed: {dataWriter.Length} Compressed: {compressedData.Length}");
        writer.PutBytesWithLength(compressedData);
    }

    private void SerializeRaw(NetDataWriter writer)
    {
        Multiplayer.Log($"CommonItemChangePacket.Serialize() Raw. Item Count: {Items.Count}");
        writer.Put(false); // uncompressed data stream
        writer.Put(Items.Count);
        foreach (var item in Items)
        {
            item.Serialize(writer);
        }
    }
}
