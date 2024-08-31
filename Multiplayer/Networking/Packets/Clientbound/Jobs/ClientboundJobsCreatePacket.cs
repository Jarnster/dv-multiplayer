using System.Collections.Generic;
using Multiplayer.Components.Networking.Jobs;
using Multiplayer.Networking.Data;
namespace Multiplayer.Networking.Packets.Clientbound.Jobs;

public class ClientboundJobUpdatePacket
{
    public ushort NetId { get; set; }
    /*
    public static ClientboundJobsCreatePacket FromNetworkedJobs(ushort stationID, NetworkedJob[] jobs)
    {
        List<JobData> jobData = new List<JobData>();
        foreach (var job in jobs)
        {
            jobData.Add(JobData.FromJob(job.NetId, job.Job));
        }

        return new ClientboundJobsCreatePacket
                                            {
                                                StationNetId = stationID,
                                                Jobs = jobData.ToArray()
                                            };
    }*/
}
