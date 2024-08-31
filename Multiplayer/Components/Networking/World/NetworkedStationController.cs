using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DV.Logic.Job;
using Multiplayer.Components.Networking.Jobs;
using Multiplayer.Components.Networking.Train;
using Multiplayer.Networking.Data;
using UnityEngine;

namespace Multiplayer.Components.Networking.World;

public class NetworkedStationController : IdMonoBehaviour<ushort, NetworkedStationController>
{
    #region Lookup Cache
    private static readonly Dictionary<StationController, NetworkedStationController> stationControllerToNetworkedStationController = new();
    private static readonly Dictionary<string, NetworkedStationController> stationIdToNetworkedStationController = new();
    private static readonly Dictionary<string, StationController> stationIdToStationController = new();
    private static readonly Dictionary<Station, NetworkedStationController> stationToNetworkedStationController = new();

    public static bool Get(ushort netId, out NetworkedStationController obj)
    {
        bool b = Get(netId, out IdMonoBehaviour<ushort, NetworkedStationController> rawObj);
        obj = (NetworkedStationController)rawObj;
        return b;
    }

    public static Dictionary<ushort, string>GetAll()
    {
        Dictionary<ushort, string> result = new Dictionary<ushort, string>();

        foreach (var kvp in stationIdToNetworkedStationController )
        {
            Multiplayer.Log($"GetAll() adding {kvp.Value.NetId}, {kvp.Key}");
            result.Add(kvp.Value.NetId, kvp.Key);
        }
        return result;
    }

    public static bool GetStationController(ushort netId, out StationController obj)
    {
        bool b = Get(netId, out NetworkedStationController networkedStationController);
        obj = b ? networkedStationController.StationController : null;
        return b;
    }
    public static bool GetFromStationId(string stationId, out NetworkedStationController networkedStationController)
    {
        return stationIdToNetworkedStationController.TryGetValue(stationId, out networkedStationController);
    }

    public static bool GetFromStation(Station station, out NetworkedStationController networkedStationController)
    {
        return stationToNetworkedStationController.TryGetValue(station, out networkedStationController);
    }
    public static bool GetStationControllerFromStationId(string stationId, out StationController stationController)
    {
        return stationIdToStationController.TryGetValue(stationId, out stationController);
    }

    public static bool GetFromStationController(StationController stationController, out NetworkedStationController networkedStationController)
    {
        return stationControllerToNetworkedStationController.TryGetValue(stationController, out networkedStationController);
    }

    public static void RegisterStationController(NetworkedStationController networkedStationController, StationController stationController)
    {
        string stationID = stationController.logicStation.ID;

        stationControllerToNetworkedStationController.Add(stationController,networkedStationController);
        stationIdToNetworkedStationController.Add(stationID, networkedStationController);
        stationIdToStationController.Add(stationID, stationController);
        stationToNetworkedStationController.Add(stationController.logicStation, networkedStationController);
}
    #endregion


    protected override bool IsIdServerAuthoritative => true;

    private StationController StationController;

    public HashSet<NetworkedJob> NetworkedJobs { get; } = new HashSet<NetworkedJob>();
    private List<NetworkedJob> NewJobs = new List<NetworkedJob>();
    private List<NetworkedJob> DirtyJobs = new List<NetworkedJob>();
    //public List<NetworkedJobOverview> JobOverviews; //for later use

    private void Awake()
    {
        base.Awake();
        StationController = GetComponent<StationController>();
        StartCoroutine(WaitForLogicStation());
    }

    private void Start()
    {
        if (NetworkLifecycle.Instance.IsHost())
        {
            NetworkLifecycle.Instance.OnTick += Server_OnTick;
        }
    }

    private IEnumerator WaitForLogicStation()
    {
        while (StationController.logicStation == null)
            yield return null;

        NetworkedStationController.RegisterStationController(this, StationController);
        Multiplayer.Log($"NetworkedStation.Awake({StationController.logicStation.ID})");
    }

    //Adding job on server
    public void AddJob(Job job)
    {
        NetworkedJob networkedJob = new GameObject($"NetworkedJob {job.ID}").AddComponent<NetworkedJob>();
        networkedJob.Job = job;
        NetworkedJobs.Add(networkedJob);
        NewJobs.Add(networkedJob);

        //Setup handlers
        job.JobTaken += OnJobTaken;
        job.JobAbandoned += OnJobAbandoned;
        job.JobCompleted += OnJobCompleted;
        job.JobExpired += OnJobExpired;
    }

    private void OnJobTaken(Job job, bool viaLoadGame)
    {

    }

    private void OnJobAbandoned(Job job)
    {

    }

    private void OnJobCompleted(Job job)
    {

    }

    private void OnJobExpired(Job job)
    {

    }

    private void Server_OnTick(uint tick)
    {
        //Send new jobs
        if (NewJobs.Count > 0)
        {
            NetworkLifecycle.Instance.Server.SendJobsCreatePacket(NetId, NewJobs.ToArray());
            NewJobs.Clear();
        }

        //Send jobs with a changed status
        if (DirtyJobs.Count > 0)
        {
            //todo send packet with updates
        }
    }


    #region Client
    public void AddJobs(JobData[] jobs)
    {
        //NetworkLifecycle.Instance.Client.Log($"AddJobs() jobs[] exists: {jobs != null}, job count: {jobs?.Count()}");

        //NetworkLifecycle.Instance.Client.Log($"AddJobs() preloop");
        foreach (JobData jobData in jobs)
        {
            //NetworkLifecycle.Instance.Client.Log($"AddJobs() inloop");

            //NetworkLifecycle.Instance.Client.Log($"AddJobs() ID: {jobData?.ID ?? ""}, netID: {jobData?.NetID}, task count: {jobData?.Tasks?.Count()}");

            // Convert TaskNetworkData to Task objects
            List<Task> tasks = new List<Task>();
            foreach (TaskNetworkData taskData in jobData.Tasks)
            {
                if (NetworkLifecycle.Instance.IsHost())
                {
                    Task test = taskData.ToTask();
                    continue;
                }

                //NetworkLifecycle.Instance.Client.Log($"AddJobs() ID: {jobData?.ID}, task type: {taskData.TaskType}");
                tasks.Add(taskData.ToTask());
            }

            //NetworkLifecycle.Instance.Client.Log($"AddJobs() ID: {jobData?.ID}, netID: {jobData?.NetID}, StationsChainData");
            // Create StationsChainData from ChainData
            StationsChainData chainData = new StationsChainData(
                jobData.ChainData.ChainOriginYardId,
                jobData.ChainData.ChainDestinationYardId
            );


            //NetworkLifecycle.Instance.Client.Log($"AddJobs() ID: {jobData?.ID}, netID: {jobData?.NetID}, newJob");
            // Create a new local Job
            Job newJob = new Job(
                tasks,
                jobData.JobType,
                jobData.TimeLimit,
                jobData.InitialWage,
                chainData,
                jobData.ID,
                jobData.RequiredLicenses
            );

            //NetworkLifecycle.Instance.Client.Log($"AddJobs() ID: {jobData?.ID}, netID: {jobData?.NetID}, properties");
            // Set additional properties
            newJob.startTime = jobData.StartTime;
            newJob.finishTime = jobData.FinishTime;
            newJob.State = jobData.State;

            //NetworkLifecycle.Instance.Client.Log($"AddJobs() ID: {jobData?.ID}, netID: {jobData?.NetID}, netjob");

            // Create a new NetworkedJob
            NetworkedJob networkedJob = new GameObject($"NetworkedJob {newJob.ID}").AddComponent<NetworkedJob>();
            networkedJob.Job = newJob;
            networkedJob.Station = this;
            networkedJob.OwnedBy = jobData.OwnedBy;

            //NetworkLifecycle.Instance.Client.Log($"AddJobs() ID: {jobData?.ID}, netID: {jobData?.NetID}, NetJob Add");
            NetworkedJobs.Add(networkedJob);

            //NetworkLifecycle.Instance.Client.Log($"AddJobs() ID: {jobData?.ID}, netID: {jobData?.NetID}, CarPlates");
            // Start coroutine to update car plates
            StartCoroutine(UpdateCarPlates(tasks, newJob.ID));

            //If the job is not owned by anyone, we can add it to the station
            //if(networkedJob.OwnedBy == Guid.Empty)
            StationController.logicStation.AddJobToStation(newJob);
            

            //start coroutine for generating overviews and booklets
            //StartCoroutine(CreatePaperWork());

            // Log the addition of the new job
            NetworkLifecycle.Instance.Client.Log($"AddJobs() {newJob?.ID} to NetworkedStationController {StationController?.logicStation?.ID}");
        }

        //allow booklets to be created
        StationController.attemptJobOverviewGeneration = true;
    }

    public void UpdateJob()
    {

    }

    public static IEnumerator UpdateCarPlates(List<DV.Logic.Job.Task> tasks, string jobId)
    {

       List<Car> cars = new List<Car>();
       UpdateCarPlatesRecursive(tasks, jobId, ref cars);


        if (cars != null)
        {
            Multiplayer.Log("NetworkedStation.UpdateCarPlates() Cars count: " + cars.Count);

            foreach (Car car in cars)
            {
                Multiplayer.Log("NetworkedStation.UpdateCarPlates() Car: " + car.ID);

                TrainCar trainCar = null;
                int loopCtr = 0;
                while (!NetworkedTrainCar.GetTrainCarFromTrainId(car.ID, out trainCar))
                {
                    loopCtr++;
                    if (loopCtr > 5000)
                    {
                        Multiplayer.Log("NetworkedStation.UpdateCarPlates() TimeOut");
                        break;
                    }
                        

                    yield return null;
                }

                trainCar?.UpdateJobIdOnCarPlates(jobId);
            }
        }
    }
    private static void UpdateCarPlatesRecursive(List<DV.Logic.Job.Task> tasks, string jobId, ref List<Car> cars)
    {
        Multiplayer.Log("NetworkedStation.UpdateCarPlatesRecursive() Starting");

        foreach (Task task in tasks)
        {
            if (task is WarehouseTask)
            {
                Multiplayer.Log("NetworkedStation.UpdateCarPlatesRecursive() WarehouseTask");
                cars = cars.Union(((WarehouseTask)task).cars).ToList();
            }
            else if (task is TransportTask)
            {
                Multiplayer.Log("NetworkedStation.UpdateCarPlatesRecursive() TransportTask");
                cars = cars.Union(((TransportTask)task).cars).ToList();
            }
            else if (task is SequentialTasks)
            {
                Multiplayer.Log("NetworkedStation.UpdateCarPlatesRecursive() SequentialTasks");
                List<Task> seqTask = new();

                for (LinkedListNode<Task> node = ((SequentialTasks)task).tasks.First; node != null; node = node.Next)
                {
                    Multiplayer.Log($"NetworkedStation.UpdateCarPlatesRecursive() SequentialTask Adding node");
                    seqTask.Add(node.Value);
                }

                Multiplayer.Log($"NetworkedStation.UpdateCarPlatesRecursive() SequentialTask Node Count:{seqTask.Count}");

                Multiplayer.Log("NetworkedStation.UpdateCarPlatesRecursive() Calling UpdateCarPlates()");
                //drill down
                UpdateCarPlatesRecursive(seqTask, jobId, ref cars);
                Multiplayer.Log($"NetworkedStation.UpdateCarPlatesRecursive() SequentialTask RETURNED");
            }
            else if (task is ParallelTasks)
            {
                //not implemented
                Multiplayer.Log("NetworkedStation.UpdateCarPlatesRecursive() ParallelTasks");

                Multiplayer.Log("NetworkedStation.UpdateCarPlatesRecursive() Calling UpdateCarPlates()");
                //drill down
                UpdateCarPlatesRecursive(((ParallelTasks)task).tasks, jobId, ref cars);
            }
            else
            {
                throw new ArgumentException("NetworkedStation.UpdateCarPlatesRecursive() Unknown task type: " + task.GetType());
            }
        }

        Multiplayer.Log("NetworkedStation.UpdateCarPlatesRecursive() Returning");
    }

    public IEnumerator CreatePaperWork()
    {
        yield return null;
    }
    #endregion
}
