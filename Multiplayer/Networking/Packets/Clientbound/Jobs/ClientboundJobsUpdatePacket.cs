using System;
using System.Collections.Generic;
using DV.ThingTypes;
using LiteNetLib.Utils;
using Multiplayer.Components.Networking.Jobs;
using Multiplayer.Networking.Data;
namespace Multiplayer.Networking.Packets.Clientbound.Jobs;

public struct JobUpdateStruct : INetSerializable
{
    public ushort JobNetID;
    public bool Invalid;
    public JobState JobState;
    public float StartTime;
    public float FinishTime;
    public Guid OwnedBy;

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(JobNetID);
        writer.Put(Invalid);

        //Invalid jobs will be deleted / deregistered
        if (Invalid)
            return;

        writer.Put((byte)JobState);
        writer.Put(StartTime);
        writer.Put(FinishTime);

        if(JobState == JobState.InProgress)
            writer.Put(OwnedBy.ToByteArray());
    }

    public void Deserialize(NetDataReader reader)
    {
        JobNetID = reader.GetUShort();
        Invalid = reader.GetBool();

        if (Invalid)
            return;

        JobState = (JobState) reader.GetByte();
        StartTime = reader.GetFloat();
        FinishTime = reader.GetFloat();
        OwnedBy = (JobState == JobState.InProgress) ? new(reader.GetBytesWithLength()) : Guid.Empty;
    }
}
public class ClientboundJobsUpdatePacket
{
    public JobUpdateStruct[] JobUpdates { get; set; }

    /*
    public static ClientboundJobsUpdatePacket FromNetworkedJobs(ushort stationID, NetworkedJob[] jobs)
    {
        List<JobData> jobData = new List<JobData>();
        foreach (var job in jobs)
        {
            jobData.Add(JobData.FromJob(job));
        }

        return new ClientboundJobsCreatePacket
                                            {
                                                StationNetId = stationID,
                                                Jobs = jobData.ToArray()
                                            };
    }
    */
}
