using System.Collections.Generic;
using System.Linq;
using DV.Logic.Job;
using DV.ThingTypes;
using LiteNetLib.Utils;
using Newtonsoft.Json;
using static DV.UI.ATutorialsMenuProvider;

namespace Multiplayer.Networking.Data;

public class JobData
{
    public ushort NetID { get; set; }
    public JobType JobType { get; set; } //serialise as byte
    public string ID { get; set; }
    public TaskNetworkData[] Tasks { get; set; }
    public StationsChainDataData ChainData { get; set; }
    public JobLicenses RequiredLicenses { get; set; } //serialise as int
    public float StartTime { get; set; }
    public float FinishTime { get; set; }
    public float InitialWage { get; set; }
    public JobState State { get; set; } //serialise as byte
    public float TimeLimit { get; set; }

    public static JobData FromJob(ushort netID, Job job)
    {
        return new JobData
        {
            NetID = netID,
            JobType = job.jobType,
            ID = job.ID,
            Tasks = TaskNetworkDataFactory.ConvertTasks(job.tasks),
            ChainData = StationsChainDataData.FromStationData(job.chainData),
            RequiredLicenses = job.requiredLicenses,
            StartTime = job.startTime,
            FinishTime = job.finishTime,
            InitialWage = job.initialWage,
            State = job.State,
            TimeLimit = job.TimeLimit
        };
    }

    public static void Serialize(NetDataWriter writer, JobData data)
    {
        Multiplayer.Log($"JobData.Serialize({data.ID}) NetID {data.NetID}");
        writer.Put(data.NetID);
        Multiplayer.Log($"JobData.Serialize({data.ID}) JobType {(byte)data.JobType}, {data.JobType}");
        writer.Put((byte)data.JobType);
        Multiplayer.Log($"JobData.Serialize({data.ID}) JobID {data.ID}");
        writer.Put(data.ID);

        Multiplayer.Log($"JobData.Serialize({data.ID}) task length {data.Tasks.Length}");
        //task data
        writer.Put((byte)data.Tasks.Length);
        foreach (var task in data.Tasks)
        {
            Multiplayer.Log($"JobData.Serialize({data.ID}) TaskType {(byte)task.TaskType}, {task.TaskType}");

            writer.Put((byte)task.TaskType);
            task.Serialize(writer);
        }

        Multiplayer.Log($"JobData.Serialize({data.ID}) calling StationsChainDataData.Serialize()");
        StationsChainDataData.Serialize(writer, data.ChainData);

        Multiplayer.Log($"JobData.Serialize({data.ID}) RequiredLicenses {data.RequiredLicenses}");
        writer.Put((int)data.RequiredLicenses);
        Multiplayer.Log($"JobData.Serialize({data.ID}) StartTime {data.StartTime}");
        writer.Put(data.StartTime);
        Multiplayer.Log($"JobData.Serialize({data.ID}) FinishTime {data.FinishTime}");
        writer.Put(data.FinishTime);
        Multiplayer.Log($"JobData.Serialize({data.ID}) InitialWage {data.InitialWage}");
        writer.Put(data.InitialWage);
        Multiplayer.Log($"JobData.Serialize({data.ID}) State {(byte)data.State}, {data.State}");
        writer.Put((byte)data.State);
        Multiplayer.Log($"JobData.Serialize({data.ID}) TimeLimit {data.TimeLimit}");
        writer.Put(data.TimeLimit);
        Multiplayer.Log(JsonConvert.SerializeObject(data, Formatting.None));
    }

    public static JobData Deserialize(NetDataReader reader)
    {
        Multiplayer.LogDebug(() => $"JobData.Deserialize(): [{string.Join(", ", reader.RawData?.Select(id => id.ToString()))}]");
        var netID = reader.GetUShort();
        Multiplayer.Log($"JobData.Deserialize() netID {netID}");
        var jobType = (JobType)reader.GetByte();
        Multiplayer.Log($"JobData.Deserialize() jobType {jobType}");
        var id = reader.GetString();
        Multiplayer.Log($"JobData.Deserialize() id {id}");

        var tasksLength = reader.GetByte();
        Multiplayer.Log($"JobData.Deserialize() tasksLength {tasksLength}");

        var tasks = new TaskNetworkData[tasksLength];
        for (int i = 0; i < tasksLength; i++)
        {
            var taskType = (TaskType)reader.GetByte();
            Multiplayer.Log($"JobData.Deserialize() taskType {taskType}");
            tasks[i] = TaskNetworkData.CreateTaskNetworkDataFromType(taskType);
            tasks[i].Deserialize(reader);
        }

        var chainData = StationsChainDataData.Deserialize(reader);
        Multiplayer.Log($"JobData.Deserialize() chainData {chainData.ChainOriginYardId}, {chainData.ChainDestinationYardId}");

        var requiredLicenses = (JobLicenses)reader.GetInt();
        Multiplayer.Log("JobData.Deserialize() requiredLicenses: " + requiredLicenses);
        var startTime = reader.GetFloat();
        Multiplayer.Log("JobData.Deserialize() startTime: " + startTime);
        var finishTime = reader.GetFloat();
        Multiplayer.Log("JobData.Deserialize() finishTime: " + finishTime);
        var initialWage = reader.GetFloat();
        Multiplayer.Log("JobData.Deserialize() initialWage: " + initialWage);
        var state = (JobState)reader.GetByte();
        Multiplayer.Log("JobData.Deserialize() state: " + state);
        var timeLimit = reader.GetFloat();
        Multiplayer.Log("JobData.Deserialize() timeLimit: " + timeLimit);

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
            TimeLimit = timeLimit
        };
    }

}

public struct StationsChainDataData
{
    public string ChainOriginYardId { get; set; }
    public string ChainDestinationYardId { get; set; }

    public static StationsChainDataData FromStationData(StationsChainData data)
    {
        return new StationsChainDataData
        {
            ChainOriginYardId = data.chainOriginYardId,
            ChainDestinationYardId = data.chainDestinationYardId
        };
    }

    public static void Serialize(NetDataWriter writer, StationsChainDataData data)
    {
        writer.Put(data.ChainOriginYardId);
        writer.Put(data.ChainDestinationYardId);
    }

    public static StationsChainDataData Deserialize(NetDataReader reader)
    {
        return new StationsChainDataData
        {
            ChainOriginYardId = reader.GetString(),
            ChainDestinationYardId = reader.GetString()
        };
    }
}
