using Multiplayer.Networking.Data;
using Multiplayer.Components.Networking.Jobs;
using Multiplayer.Components.Networking.World;
using System.Collections.Generic;

namespace Multiplayer.Networking.Packets.Clientbound.Jobs;
public class ClientboundJobsUpdatePacket
{
    public ushort StationNetId { get; set; }
    public JobUpdateStruct[] JobUpdates { get; set; }

    
    public static ClientboundJobsUpdatePacket FromNetworkedJobs(ushort stationNetID, NetworkedJob[] jobs)
    {
        Multiplayer.Log($"ClientboundJobsUpdatePacket.FromNetworkedJobs({stationNetID}, {jobs.Length})");

        List<JobUpdateStruct> jobData = new List<JobUpdateStruct>();
        foreach (var job in jobs)
        {
            ushort validationNetId = 0;

            if (NetworkedStationController.GetFromJobValidator(job.JobValidator, out NetworkedStationController netValidationStation))
                validationNetId = netValidationStation.NetId;

            JobUpdateStruct data = new JobUpdateStruct
            {
                JobNetID = job.NetId,
                JobState = job.Job.State,
                StartTime = job.Job.startTime,
                FinishTime = job.Job.finishTime,
                ItemNetID = job.ValidationItem.NetId,
                ValidationStationId = validationNetId
            };

            jobData.Add(data);
        }

        return new ClientboundJobsUpdatePacket
                                                {
                                                    StationNetId = stationNetID,
                                                    JobUpdates = jobData.ToArray()
                                                };
    }
}
