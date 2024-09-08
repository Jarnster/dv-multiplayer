using DV.ThingTypes;
using LiteNetLib.Utils;

namespace Multiplayer.Networking.Packets.Clientbound.Jobs;

public struct JobUpdateStruct
{
    public ushort JobNetID;
    public bool Invalid;
    public JobState JobState;
    public float StartTime;
    public float FinishTime;
    public ushort OwnedBy;

    public static void Serialize(NetDataWriter writer, JobUpdateStruct data)
    {
        writer.Put(data.JobNetID);
        writer.Put(data.Invalid);

        //Invalid jobs will be deleted / deregistered
        if (data.Invalid)
            return;

        writer.Put((byte)data.JobState);
        writer.Put(data.StartTime);
        writer.Put(data.FinishTime);

        writer.Put(data.OwnedBy);
    }

    public static JobUpdateStruct Deserialize(NetDataReader reader)
    {
        JobUpdateStruct deserialised = new JobUpdateStruct();

        deserialised.JobNetID = reader.GetUShort();
        deserialised.Invalid = reader.GetBool();

        if (deserialised.Invalid)
            return deserialised;

        deserialised.JobState = (JobState) reader.GetByte();
        deserialised.StartTime = reader.GetFloat();
        deserialised.FinishTime = reader.GetFloat();
        deserialised.OwnedBy = reader.GetUShort();

        return deserialised;
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
