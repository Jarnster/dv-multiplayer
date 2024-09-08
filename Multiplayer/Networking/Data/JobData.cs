using DV.Logic.Job;
using DV.ThingTypes;
using LiteNetLib.Utils;
using Multiplayer.Components.Networking;
using Multiplayer.Components.Networking.Jobs;
using System;

namespace Multiplayer.Networking.Data;

public class JobData
{
    public ushort NetID { get; set; }
    public JobType JobType { get; set; } //serialise as byte
    public string ID { get; set; }
    public TaskNetworkData[] Tasks { get; set; }
    public StationsChainNetworkData ChainData { get; set; }
    public JobLicenses RequiredLicenses { get; set; } //serialise as int
    public float StartTime { get; set; }
    public float FinishTime { get; set; }
    public float InitialWage { get; set; }
    public JobState State { get; set; } //serialise as byte
    public float TimeLimit { get; set; }
    public int PlayerId { get; set; }

    public static JobData FromJob(NetworkedJob networkedJob)
    {
        Job job = networkedJob.Job;

        return new JobData
        {
            NetID = networkedJob.NetId,
            JobType = job.jobType,
            ID = job.ID,
            Tasks = TaskNetworkDataFactory.ConvertTasks(job.tasks),
            ChainData = StationsChainNetworkData.FromStationData(job.chainData),
            RequiredLicenses = job.requiredLicenses,
            StartTime = job.startTime,
            FinishTime = job.finishTime,
            InitialWage = job.initialWage,
            State = job.State,
            TimeLimit = job.TimeLimit,
            PlayerId = networkedJob.playerID
        };
    }

    public static void Serialize(NetDataWriter writer, JobData data)
    {
        NetworkLifecycle.Instance.Server.Log($"JobData.Serialize({data.ID}) NetID {data.NetID}");
        writer.Put(data.NetID);
        //Multiplayer.Log($"JobData.Serialize({data.ID}) JobType {(byte)data.JobType}, {data.JobType}");
        writer.Put((byte)data.JobType);
        //Multiplayer.Log($"JobData.Serialize({data.ID}) JobID {data.ID}");
        writer.Put(data.ID);

        //Multiplayer.Log($"JobData.Serialize({data.ID}) task length {data.Tasks.Length}");
        //task data
        writer.Put((byte)data.Tasks.Length);
        foreach (var task in data.Tasks)
        {
            //Multiplayer.Log($"JobData.Serialize({data.ID}) TaskType {(byte)task.TaskType}, {task.TaskType}");

            writer.Put((byte)task.TaskType);
            task.Serialize(writer);
        }

        //Multiplayer.Log($"JobData.Serialize({data.ID}) calling StationsChainDataData.Serialize()");
        StationsChainNetworkData.Serialize(writer, data.ChainData);

        //Multiplayer.Log($"JobData.Serialize({data.ID}) RequiredLicenses {data.RequiredLicenses}");
        writer.Put((int)data.RequiredLicenses);
        //Multiplayer.Log($"JobData.Serialize({data.ID}) StartTime {data.StartTime}");
        writer.Put(data.StartTime);
        //Multiplayer.Log($"JobData.Serialize({data.ID}) FinishTime {data.FinishTime}");
        writer.Put(data.FinishTime);
        //Multiplayer.Log($"JobData.Serialize({data.ID}) InitialWage {data.InitialWage}");
        writer.Put(data.InitialWage);
        //Multiplayer.Log($"JobData.Serialize({data.ID}) State {(byte)data.State}, {data.State}");
        writer.Put((byte)data.State);
        //Multiplayer.Log($"JobData.Serialize({data.ID}) TimeLimit {data.TimeLimit}");
        writer.Put(data.TimeLimit);
        //Multiplayer.Log(JsonConvert.SerializeObject(data, Formatting.None));

        //Take on the GUID of the player
        //if(data.State != JobState.Available)
        //    writer.Put(data.OwnedBy.ToByteArray());

        writer.Put(data.PlayerId);
    }

    public static JobData Deserialize(NetDataReader reader)
    {
        //Multiplayer.LogDebug(() => $"JobData.Deserialize(): [{string.Join(", ", reader.RawData?.Select(id => id.ToString()))}]");
        ushort netID = reader.GetUShort();
        //Multiplayer.Log($"JobData.Deserialize() netID {netID}");
        JobType jobType = (JobType)reader.GetByte();
        //Multiplayer.Log($"JobData.Deserialize() jobType {jobType}");
        string id = reader.GetString();
        //Multiplayer.Log($"JobData.Deserialize() id {id}");

        byte tasksLength = reader.GetByte();
        //Multiplayer.Log($"JobData.Deserialize() tasksLength {tasksLength}");

        TaskNetworkData[] tasks = new TaskNetworkData[tasksLength];
        for (int i = 0; i < tasksLength; i++)
        {
            TaskType taskType = (TaskType)reader.GetByte();
            //Multiplayer.Log($"JobData.Deserialize() taskType {taskType}");
            tasks[i] = TaskNetworkDataFactory.ConvertTask(taskType);
            //Multiplayer.Log($"JobData.Deserialize() TaskNetworkData not null: {tasks[i] != null}, {tasks[i].GetType().FullName}");
            tasks[i].Deserialize(reader);
            //Multiplayer.Log($"JobData.Deserialize() TaskNetworkData Deserialised");
        }

        StationsChainNetworkData chainData = StationsChainNetworkData.Deserialize(reader);
        //Multiplayer.Log($"JobData.Deserialize() chainData {chainData.ChainOriginYardId}, {chainData.ChainDestinationYardId}");

        JobLicenses requiredLicenses = (JobLicenses)reader.GetInt();
        //Multiplayer.Log("JobData.Deserialize() requiredLicenses: " + requiredLicenses);
        float startTime = reader.GetFloat();
        //Multiplayer.Log("JobData.Deserialize() startTime: " + startTime);
        float finishTime = reader.GetFloat();
        //Multiplayer.Log("JobData.Deserialize() finishTime: " + finishTime);
        float initialWage = reader.GetFloat();
        //Multiplayer.Log("JobData.Deserialize() initialWage: " + initialWage);
        JobState state = (JobState)reader.GetByte();
        //Multiplayer.Log("JobData.Deserialize() state: " + state);
        float timeLimit = reader.GetFloat();
        //Multiplayer.Log("JobData.Deserialize() timeLimit: " + timeLimit);

        //int playerId =  (state != JobState.Available)? new(reader.GetBytesWithLength()) : Guid.Empty;
        int playerId = reader.GetInt();

        return new JobData
        {
            NetID = netID,
            JobType = jobType,
            ID = id,
            Tasks = tasks,
            ChainData = chainData,
            RequiredLicenses = requiredLicenses,
            StartTime = startTime,
            FinishTime = finishTime,
            InitialWage = initialWage,
            State = state,
            TimeLimit = timeLimit,
            PlayerId = playerId,
        };
    }

}

public struct StationsChainNetworkData
{
    public string ChainOriginYardId { get; set; }
    public string ChainDestinationYardId { get; set; }

    public static StationsChainNetworkData FromStationData(StationsChainData data)
    {
        return new StationsChainNetworkData
        {
            ChainOriginYardId = data.chainOriginYardId,
            ChainDestinationYardId = data.chainDestinationYardId
        };
    }

    public static void Serialize(NetDataWriter writer, StationsChainNetworkData data)
    {
        writer.Put(data.ChainOriginYardId);
        writer.Put(data.ChainDestinationYardId);
    }

    public static StationsChainNetworkData Deserialize(NetDataReader reader)
    {
        return new StationsChainNetworkData
        {
            ChainOriginYardId = reader.GetString(),
            ChainDestinationYardId = reader.GetString()
        };
    }
}
