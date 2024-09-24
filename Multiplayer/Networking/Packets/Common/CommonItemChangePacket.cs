using LiteNetLib.Utils;
using Multiplayer.Networking.Data;
using System.IO;
using DV.Logic.Job;
using System.Collections.Generic;
using System.Linq;
using System;


namespace Multiplayer.Networking.Packets.Common;

public class CommonItemChangePacket : INetSerializable
{
    private const int COMPRESS_AFTER_COUNT = 50;

    public List<ItemUpdateData> Items = new List<ItemUpdateData>();

    public void Deserialize(NetDataReader reader)
    {
        Multiplayer.Log("CommonItemChangePacket.Deserialize()");
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
        }
        catch (Exception ex)
        {
            Multiplayer.LogError($"Error in CommonItemChangePacket.Deserialize: {ex.Message}");
        }
    }

    private void DeserializeCompressed(NetDataReader reader)
    {
        Multiplayer.Log("CommonItemChangePacket.DeserializeCompressed()");
        byte[] compressedData = reader.GetBytesWithLength();
        byte[] decompressedData = PacketCompression.Decompress(compressedData);
        Multiplayer.Log($"Compressed: {compressedData.Length} Decompressed: {decompressedData.Length}");

        NetDataReader decompressedReader = new NetDataReader(decompressedData);
        int itemCount = decompressedReader.GetInt();
        Items.Capacity = itemCount;
        for (int i = 0; i < itemCount; i++)
        {
            var item = new ItemUpdateData();
            item.Deserialize(decompressedReader);
            Items.Add(item);
        }
    }

    private void DeserializeRaw(NetDataReader reader)
    {
        Multiplayer.Log("CommonItemChangePacket.DeserializeRaw()");
        int itemCount = reader.GetInt();
        Items.Capacity = itemCount;
        for (int i = 0; i < itemCount; i++)
        {
            var item = new ItemUpdateData();
            item.Deserialize(reader);
            Items.Add(item);
        }
    }

    public void Serialize(NetDataWriter writer)
    {
        Multiplayer.Log("CommonItemChangePacket.Serialize()");
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

        NetDataWriter dataWriter = new NetDataWriter();

        dataWriter.Put(Items.Count);
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
