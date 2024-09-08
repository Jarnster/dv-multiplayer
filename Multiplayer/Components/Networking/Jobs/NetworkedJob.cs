using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DV.Logic.Job;
using Multiplayer.Components.Networking.World;
using Multiplayer.Networking.Packets.Clientbound.Jobs;
using UnityEngine;


namespace Multiplayer.Components.Networking.Jobs;

public class NetworkedJob : IdMonoBehaviour<ushort, NetworkedJob>
{
    #region Lookup Cache

    private static readonly Dictionary<Job, NetworkedJob> jobToNetworkedJob = new();
    private static readonly Dictionary<string, NetworkedJob> jobIdToNetworkedJob = new();
    private static readonly Dictionary<string, Job> jobIdToJob = new();

    public static bool Get(ushort netId, out NetworkedJob obj)
    {
        bool b = Get(netId, out IdMonoBehaviour<ushort, NetworkedJob> rawObj);
        obj = (NetworkedJob)rawObj;
        return b;
    }

    public static bool GetJob(ushort netId, out Job obj)
    {
        bool b = Get(netId, out NetworkedJob networkedJob);
        obj = b ? networkedJob.Job : null;
        return b;
    }


    //public static NetworkedJob GetFromJob(Job job)
    //{
    //    return jobToNetworkedJob[job];
    //}

    public static bool TryGetFromJob(Job job, out NetworkedJob networkedJob)
    {
        return jobToNetworkedJob.TryGetValue(job, out networkedJob);
    }

    public static bool TryGetFromJobId(string jobId, out NetworkedJob networkedJob)
    {
        return jobIdToNetworkedJob.TryGetValue(jobId, out networkedJob);
    }

    #endregion
    protected override bool IsIdServerAuthoritative => true;

    public Job Job;
    public JobOverview JobOverview;
    public JobBooklet JobBooklet;
    public NetworkedStationController Station;

    public Guid OwnedBy = Guid.Empty; //GUID of player who took the job (sever only)
    public int playerID;              //ID of player who took the job (client & server)

    public JobValidator JobValidator; //Job validator to print the booklet/job validation at (client only)
    public bool ValidatorRequestSent = false;
    public bool ValidatorResponseReceived = false;
    public bool ValidationAccepted = false;
    public ValidationType ValidationType;
    public NetworkedItem ValidationItem;

    #region Client



    #endregion


    private void Start()
    {
        //startup stuff
        Multiplayer.Log($"NetworkedJob.Start({Job.ID})");

        jobToNetworkedJob[Job] = this;
        jobIdToNetworkedJob[Job.ID] = this;
        jobIdToJob[Job.ID] = Job;
         
        Multiplayer.Log("NetworkedJob.Start() Started");
    }

    private void OnDisable()
    {
        if (UnloadWatcher.isQuitting)
            return;


        if (UnloadWatcher.isUnloading)
            return;

        jobToNetworkedJob.Remove(Job);
        jobIdToNetworkedJob.Remove(Job.ID);
        jobIdToNetworkedJob.Remove(Job.ID);

        Destroy(this);
    }

    #region Server

    private void Server_OnTick(uint tick)
    {
        if (UnloadWatcher.isUnloading)
            return;

    }

    #endregion

    #region Common

    private void Common_OnTick(uint tick)
    {
        if (UnloadWatcher.isUnloading)
            return;
    }

    #endregion

    #region Client

    #endregion
}
