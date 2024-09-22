using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DV.Booklets;
using DV.Logic.Job;
using DV.ServicePenalty;
using DV.Utils;
using Multiplayer.Components.Networking.Jobs;
using Multiplayer.Components.Networking.Train;
using Multiplayer.Networking.Data;
using Multiplayer.Utils;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Assertions.Must;


namespace Multiplayer.Components.Networking.World;

public class NetworkedStationController : IdMonoBehaviour<ushort, NetworkedStationController>
{
    #region Lookup Cache
    private static readonly Dictionary<StationController, NetworkedStationController> stationControllerToNetworkedStationController = new();
    private static readonly Dictionary<string, NetworkedStationController> stationIdToNetworkedStationController = new();
    private static readonly Dictionary<string, StationController> stationIdToStationController = new();
    private static readonly Dictionary<Station, NetworkedStationController> stationToNetworkedStationController = new();
    private static readonly Dictionary<JobValidator, NetworkedStationController> jobValidatorToNetworkedStation = new();
    private static readonly List<JobValidator> jobValidators = new List<JobValidator>();

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

    public static bool GetFromJobValidator(JobValidator jobValidator, out NetworkedStationController networkedStationController)
    {
        if (jobValidator == null)
        {
            networkedStationController = null;
            return false;
        }

        return jobValidatorToNetworkedStation.TryGetValue(jobValidator, out networkedStationController);
    }

    public static void RegisterStationController(NetworkedStationController networkedStationController, StationController stationController)
    {
        string stationID = stationController.logicStation.ID;

        stationControllerToNetworkedStationController.Add(stationController,networkedStationController);
        stationIdToNetworkedStationController.Add(stationID, networkedStationController);
        stationIdToStationController.Add(stationID, stationController);
        stationToNetworkedStationController.Add(stationController.logicStation, networkedStationController);
    }

    public static void QueueJobValidator(JobValidator jobValidator)
    {
        Multiplayer.Log($"QueueJobValidator() {jobValidator.transform.parent.name}");

        jobValidators.Add(jobValidator);
    }

    private static void RegisterJobValidator(JobValidator jobValidator, NetworkedStationController stationController)
    {
        Multiplayer.Log($"RegisterJobValidator() {jobValidator.transform.parent.name}, {stationController.name}");
        stationController.JobValidator = jobValidator;
        jobValidatorToNetworkedStation[jobValidator] = stationController;
    }
    #endregion


    protected override bool IsIdServerAuthoritative => true;

    public StationController StationController;

    public JobValidator JobValidator;

    public HashSet<NetworkedJob> NetworkedJobs { get; } = new HashSet<NetworkedJob>();
    private List<NetworkedJob> NewJobs = new List<NetworkedJob>();
    private List<NetworkedJob> DirtyJobs = new List<NetworkedJob>();

    private List<Job> availableJobs;
    private List<Job> takenJobs;
    private List<Job> abandonedJobs;
    private List<Job> completedJobs;


    protected override void Awake()
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

        RegisterStationController(this, StationController);

        availableJobs = StationController.logicStation.availableJobs;
        takenJobs = StationController.logicStation.takenJobs;
        abandonedJobs = StationController.logicStation.abandonedJobs;
        completedJobs = StationController.logicStation.completedJobs;

        Multiplayer.Log($"NetworkedStation.Awake({StationController.logicStation.ID})");

        foreach (JobValidator validator in jobValidators)
        {
            string stationName = validator.transform.parent.name ?? "";
            stationName += "_office_anchor";

            if(this.transform.parent.name.Equals(stationName, StringComparison.OrdinalIgnoreCase))
            {
                JobValidator = validator;
                RegisterJobValidator(validator, this);
                jobValidators.Remove(validator);
                break;
            }
        }
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

        networkedJob.OverviewGenerated += OnOverviewGeneration;
    }

    public void OnOverviewGeneration(NetworkedJob job)
    {
        if(!DirtyJobs.Contains(job))
            DirtyJobs.Add(job);

    }

    private void OnJobTaken(Job job, bool viaLoadGame)
    {
        if (viaLoadGame)
            return;

        Multiplayer.Log($"NetworkedStationController.OnJobTaken({job.ID})");
        if(NetworkedJob.TryGetFromJob(job, out NetworkedJob networkedJob))
            DirtyJobs.Add(networkedJob);
    }

    private void OnJobAbandoned(Job job)
    {
        if (NetworkedJob.TryGetFromJob(job, out NetworkedJob networkedJob))
            DirtyJobs.Add(networkedJob);
    }

    private void OnJobCompleted(Job job)
    {
        if (NetworkedJob.TryGetFromJob(job, out NetworkedJob networkedJob))
            DirtyJobs.Add(networkedJob);
    }

    private void OnJobExpired(Job job)
    {
        if (NetworkedJob.TryGetFromJob(job, out NetworkedJob networkedJob))
            DirtyJobs.Add(networkedJob);
    }

    private void Server_OnTick(uint tick)
    {
        //Send new jobs
        if (NewJobs.Count > 0)
        {
            NetworkLifecycle.Instance.Server.SendJobsCreatePacket(this, NewJobs.ToArray());
            NewJobs.Clear();
        }

        //Send jobs with a changed status
        if (DirtyJobs.Count > 0)
        {
            //todo send packet with updates
            NetworkLifecycle.Instance.Server.SendJobsUpdatePacket(NetId, DirtyJobs.ToArray());
            DirtyJobs.Clear();
        }
    }


    #region Client
    public void AddJobs(JobData[] jobs)
    {
        NetworkLifecycle.Instance.Client.Log($"AddJobs() jobs[] exists: {jobs != null}, job count: {jobs?.Count()}");

        foreach (JobData job in jobs)
        {
            NetworkLifecycle.Instance.Client.Log($"AddJobs() inloop");
            NetworkLifecycle.Instance.Client.Log($"AddJobs() ID: {job?.ID ?? ""}, netID: {job?.NetID}, task count: {job?.Tasks?.Count()}");

            // Convert TaskNetworkData to Task objects
            List<Task> tasks = new List<Task>();
            foreach (TaskNetworkData taskData in job.Tasks)
            {
                if (NetworkLifecycle.Instance.IsHost())
                {
                    Task test = taskData.ToTask();
                    continue;
                }

                NetworkLifecycle.Instance.Client.Log($"AddJobs() ID: {job?.ID}, task type: {taskData.TaskType}");
                tasks.Add(taskData.ToTask());
            }

            NetworkLifecycle.Instance.Client.Log($"AddJobs() ID: {job?.ID}, netID: {job?.NetID}, StationsChainData");
            // Create StationsChainData from ChainData
            StationsChainData chainData = new StationsChainData(
                job.ChainData.ChainOriginYardId,
                job.ChainData.ChainDestinationYardId
            );


            NetworkLifecycle.Instance.Client.Log($"AddJobs() ID: {job?.ID}, netID: {job?.NetID}, newJob");
            // Create a new local Job
            Job newJob = new Job(
                tasks,
                job.JobType,
                job.TimeLimit,
                job.InitialWage,
                chainData,
                job.ID,
                job.RequiredLicenses
            );

            NetworkLifecycle.Instance.Client.Log($"AddJobs() ID: {job?.ID}, netID: {job?.NetID}, properties");
            // Set additional properties
            newJob.startTime = job.StartTime;
            newJob.finishTime = job.FinishTime;
            newJob.State = job.State;

            NetworkLifecycle.Instance.Client.Log($"AddJobs() ID: {job?.ID}, netID: {job?.NetID}, netjob");

            // Create a new NetworkedJob
            NetworkedJob networkedJob = new GameObject($"NetworkedJob {newJob.ID}").AddComponent<NetworkedJob>();
            networkedJob.NetId = job.NetID;
            networkedJob.Job = newJob;
            networkedJob.Station = this;
            //networkedJob.playerID = job.PlayerId;

            NetworkLifecycle.Instance.Client.Log($"AddJobs() ID: {job?.ID}, netID: {job?.NetID}, NetJob Add");
            NetworkedJobs.Add(networkedJob);

            NetworkLifecycle.Instance.Client.Log($"AddJobs() ID: {job?.ID}, netID: {job?.NetID}, CarPlates");
            // Start coroutine to update car plates
            StartCoroutine(UpdateCarPlates(tasks, newJob.ID));

            //If the job is not owned by anyone, we can add it to the station
            //if(networkedJob.OwnedBy == Guid.Empty)
            NetworkLifecycle.Instance.Client.Log($"AddJobs() ID: {job?.ID}, netID: {job?.NetID}, AddJobToStation()");
            StationController.logicStation.AddJobToStation(newJob);
            StationController.processedNewJobs.Add(newJob);

            NetworkLifecycle.Instance.Client.Log($"AddJobs() ID: {job?.ID}, netID: {job?.NetID}, job state {job.State}, itemNetId {job.ItemNetID}");

            //start coroutine for generating overviews and booklets
            NetworkLifecycle.Instance.Client.Log($"AddJobs() {newJob?.ID} Generating Overview {(newJob.State == DV.ThingTypes.JobState.Available && job.ItemNetID != 0)}");
            if (newJob.State == DV.ThingTypes.JobState.Available && job.ItemNetID != 0)
                GenerateOverview(networkedJob, job.ItemNetID, job.ItemPosition);

            // Log the addition of the new job
            NetworkLifecycle.Instance.Client.Log($"AddJobs() {newJob?.ID} to NetworkedStationController {StationController?.logicStation?.ID}");
        }

        //allow booklets to be created
        StationController.attemptJobOverviewGeneration = true;
    }

    public void UpdateJobs(JobUpdateStruct[] jobs)
    {
        foreach (JobUpdateStruct job in jobs)
        {
            if (!NetworkedJob.Get(job.JobNetID, out NetworkedJob netJob))
                continue;

            JobValidator validator = null;
            if(job.ItemNetID != 0 &&  job.ValidationStationId != 0)
                if (Get(job.ValidationStationId, out var netStation))
                    validator = netStation.JobValidator;

            Multiplayer.Log($"NetworkedStation.UpdateJobs() jobNetId: {job.JobNetID}, Validator found: {validator != null}");

            //state change updates
            if (netJob.Job.State != job.JobState)
            {
                netJob.Job.State = job.JobState;
                bool printed = false;

                switch (netJob.Job.State)
                {
                    case DV.ThingTypes.JobState.InProgress:
                        availableJobs.Remove(netJob.Job);
                        takenJobs.Add(netJob.Job);

                        netJob.JobBooklet = BookletCreator.CreateJobBooklet(netJob.Job, validator.bookletPrinter.spawnAnchor.position, validator.bookletPrinter.spawnAnchor.rotation, WorldMover.OriginShiftParent, true);

                        netJob.ValidationItem.NetId = job.ItemNetID;
                        printed = true;
                        netJob.JobOverview.DestroyJobOverview();
                        break;
                    case DV.ThingTypes.JobState.Completed:
                        takenJobs.Remove(netJob.Job);
                        completedJobs.Add(netJob.Job);

                        DisplayableDebt displayableDebt = SingletonBehaviour<JobDebtController>.Instance.LastStagedJobDebt;
                        netJob.JobReport = BookletCreator.CreateJobReport(netJob.Job, displayableDebt, validator.bookletPrinter.spawnAnchor.position, validator.bookletPrinter.spawnAnchor.rotation, WorldMover.OriginShiftParent);

                        netJob.ValidationItem.NetId = job.ItemNetID;
                        printed = true;
                        netJob.JobBooklet.DestroyJobBooklet();
                        break;
                    case DV.ThingTypes.JobState.Abandoned:
                        takenJobs.Remove(netJob.Job);
                        abandonedJobs.Add(netJob.Job);

                        //netJob.JobExpiredReport = BookletCreator.CreateJobExpiredReport(netJob.Job, validator.bookletPrinter.spawnAnchor.position, validator.bookletPrinter.spawnAnchor.rotation, WorldMover.OriginShiftParent);
                        //netJob.ValidationItem.NetId = job.ItemNetID;
                        //printed = true;

                        break;
                    case DV.ThingTypes.JobState.Expired:
                        if(availableJobs.Contains(netJob.Job))
                            availableJobs.Remove(netJob.Job);

                        //netJob.JobExpiredReport = BookletCreator.CreateJobExpiredReport(netJob.Job, validator.bookletPrinter.spawnAnchor.position, validator.bookletPrinter.spawnAnchor.rotation, WorldMover.OriginShiftParent);
                        //netJob.ValidationItem.NetId = job.ItemNetID;
                        //printed = true;

                        break;
                    default:
                        NetworkLifecycle.Instance.Client.LogError($"NetworkedStation.UpdateJobs() Unrecognised Job State for JobId: {job.JobNetID}, {netJob.Job.ID}");
                        break;
                }

                if (printed)
                {
                    Multiplayer.Log($"NetworkedStation.UpdateJobs() jobNetId: {job.JobNetID}, Playing sounds");
                    netJob.ValidatorResponseReceived = true;
                    netJob.ValidationAccepted = true;
                    validator.jobValidatedSound.Play(validator.bookletPrinter.spawnAnchor.position, 1f, 1f, 0f, 1f, 500f, default(AudioSourceCurves), null, validator.transform, false, 0f, null);
                    validator.bookletPrinter.Print(false);
                }
            }

            //job overview generation update
            if(job.JobState == DV.ThingTypes.JobState.Available && job.ItemNetID !=0)
            {
                
                if (netJob.JobOverview == null)
                {
                    //create overview
                    Multiplayer.LogDebug(()=>$"NetworkedStation.UpdateJobs() Creating JobOverview");
                    if (job.JobState == DV.ThingTypes.JobState.Available && job.ItemNetID != 0)
                        GenerateOverview(netJob, job.ItemNetID, job.ItemPositionData);
                }
                else
                {
                    Multiplayer.LogDebug(() => $"NetworkedStation.UpdateJobs() Setting JobOverview");
                    netJob.ValidationItem.NetId = job.ItemNetID;
                }
            }

            //generic update
            netJob.Job.startTime = job.StartTime;
            netJob.Job.finishTime = job.FinishTime;
        }
    }

    public void RemoveJob(NetworkedJob job)
    {
        if (availableJobs.Contains(job.Job))
            availableJobs.Remove(job.Job);

        if (takenJobs.Contains(job.Job))
            takenJobs.Remove(job.Job);

        if (completedJobs.Contains(job.Job))
            completedJobs.Remove(job.Job);

        if (abandonedJobs.Contains(job.Job))
            abandonedJobs.Remove(job.Job);

        job.JobOverview?.DestroyJobOverview();
        job.JobBooklet?.DestroyJobBooklet();

        NetworkedJobs.Remove(job);
        GameObject.Destroy(job);
    }

    public static IEnumerator UpdateCarPlates(List<DV.Logic.Job.Task> tasks, string jobId)
    {

       List<Car> cars = new List<Car>();
       UpdateCarPlatesRecursive(tasks, jobId, ref cars);


        if (cars != null)
        {
            //Multiplayer.Log("NetworkedStation.UpdateCarPlates() Cars count: " + cars.Count);

            foreach (Car car in cars)
            {
                //Multiplayer.Log("NetworkedStation.UpdateCarPlates() Car: " + car.ID);

                TrainCar trainCar = null;
                int loopCtr = 0;
                while (!NetworkedTrainCar.GetTrainCarFromTrainId(car.ID, out trainCar))
                {
                    loopCtr++;
                    if (loopCtr > 5000)
                    {
                        //Multiplayer.Log("NetworkedStation.UpdateCarPlates() TimeOut");
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
        //Multiplayer.Log("NetworkedStation.UpdateCarPlatesRecursive() Starting");

        foreach (Task task in tasks)
        {
            if (task is WarehouseTask)
            {
                //Multiplayer.Log("NetworkedStation.UpdateCarPlatesRecursive() WarehouseTask");
                cars = cars.Union(((WarehouseTask)task).cars).ToList();
            }
            else if (task is TransportTask)
            {
                //Multiplayer.Log("NetworkedStation.UpdateCarPlatesRecursive() TransportTask");
                cars = cars.Union(((TransportTask)task).cars).ToList();
            }
            else if (task is SequentialTasks)
            {
                //Multiplayer.Log("NetworkedStation.UpdateCarPlatesRecursive() SequentialTasks");
                List<Task> seqTask = new();

                for (LinkedListNode<Task> node = ((SequentialTasks)task).tasks.First; node != null; node = node.Next)
                {
                    //Multiplayer.Log($"NetworkedStation.UpdateCarPlatesRecursive() SequentialTask Adding node");
                    seqTask.Add(node.Value);
                }
                //drill down
                UpdateCarPlatesRecursive(seqTask, jobId, ref cars);
                //Multiplayer.Log($"NetworkedStation.UpdateCarPlatesRecursive() SequentialTask RETURNED");
            }
            else if (task is ParallelTasks)
            {
                //drill down
                UpdateCarPlatesRecursive(((ParallelTasks)task).tasks, jobId, ref cars);
            }
            else
            {
                throw new ArgumentException("NetworkedStation.UpdateCarPlatesRecursive() Unknown task type: " + task.GetType());
            }
        }

        //Multiplayer.Log("NetworkedStation.UpdateCarPlatesRecursive() Returning");
    }

    private void GenerateOverview(NetworkedJob networkedJob, ushort itemNetId, ItemPositionData posData)
    {
        networkedJob.JobOverview = BookletCreator_JobOverview.Create(networkedJob.Job, posData.Position + WorldMover.currentMove, posData.Rotation);
        NetworkedItem netItem = networkedJob.JobOverview.GetOrAddComponent<NetworkedItem>();
        netItem.NetId = itemNetId;
        networkedJob.ValidationItem = netItem;
        StationController.spawnedJobOverviews.Add(networkedJob.JobOverview);
    }

    private void OnDisable()
    {

        if (UnloadWatcher.isQuitting)
            return;

        NetworkLifecycle.Instance.OnTick -= Server_OnTick;

        string stationId = StationController.logicStation.ID;
 
        stationControllerToNetworkedStationController.Remove(StationController);
        stationIdToNetworkedStationController.Remove(stationId);
        stationIdToStationController.Remove(stationId);
        stationToNetworkedStationController.Remove(StationController.logicStation);
        jobValidatorToNetworkedStation.Remove(JobValidator);
        jobValidators.Remove(this.JobValidator);

        Destroy(this);

    }
    #endregion
}
