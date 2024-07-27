using System;
using System.Collections.Generic;
using System.Linq;
using DV;
using DV.InventorySystem;
using DV.Logic.Job;
using DV.Scenarios.Common;
using DV.ServicePenalty;
using DV.ThingTypes;
using DV.WeatherSystem;
using Humanizer;
using LiteNetLib;
using LiteNetLib.Utils;
using Multiplayer.Components.Networking;
using Multiplayer.Components.Networking.Train;
using Multiplayer.Components.Networking.World;
using Multiplayer.Components.Networking.Jobs;
using Multiplayer.Networking.Data;
using Multiplayer.Networking.Managers.Server;
using Multiplayer.Networking.Packets.Clientbound;
using Multiplayer.Networking.Packets.Clientbound.Jobs;
using Multiplayer.Networking.Packets.Clientbound.SaveGame;
using Multiplayer.Networking.Packets.Clientbound.Train;
using Multiplayer.Networking.Packets.Clientbound.World;
using Multiplayer.Networking.Packets.Common;
using Multiplayer.Networking.Packets.Common.Train;
using Multiplayer.Networking.Packets.Serverbound;
using Multiplayer.Utils;
using UnityEngine;
using UnityModManagerNet;
using System.Net;

namespace Multiplayer.Networking.Listeners;

public class NetworkServer : NetworkManager
{
    protected override string LogPrefix => "[Server]";

    private readonly Queue<NetPeer> joinQueue = new();
    private readonly Dictionary<byte, ServerPlayer> serverPlayers = new();
    private readonly Dictionary<byte, NetPeer> netPeers = new();

    private LobbyServerManager lobbyServerManager;
    public bool isPublic;
    public bool isSinglePlayer;
    public LobbyServerData serverData;

    public IReadOnlyCollection<ServerPlayer> ServerPlayers => serverPlayers.Values;
    public int PlayerCount => netManager.ConnectedPeersCount;

    private static NetPeer selfPeer => NetworkLifecycle.Instance.Client?.selfPeer;
    public static byte SelfId => (byte)selfPeer.Id;
    private readonly ModInfo[] serverMods;

    public readonly IDifficulty Difficulty;
    private bool IsLoaded;

    public NetworkServer(IDifficulty difficulty, Settings settings, bool isPublic, bool isSinglePlayer, LobbyServerData serverData) : base(settings)
    {
        this.isPublic = isPublic;
        this.isSinglePlayer = isSinglePlayer;
        this.serverData = serverData;

        Difficulty = difficulty;
        serverMods = ModInfo.FromModEntries(UnityModManager.modEntries);
    }

    public bool Start(int port)
    {
        WorldStreamingInit.LoadingFinished += OnLoaded;

        //Try to get our static IPv6 Address we will need this for IPv6 NAT punching to be reliable
        if(IPAddress.TryParse(LobbyServerManager.GetStaticIPv6Address(), out IPAddress ipv6Address))
        {
            Multiplayer.Log($"Starting server, will listen to IPv6: {ipv6Address.ToString()}");
            //start the connection, IPv4 messages can come from anywhere, IPv6 messages need to specifically come from the static IPv6
            return netManager.Start(IPAddress.Any, ipv6Address,port);
        }

        //we're not running IPv6, start as normal
        return netManager.Start(port);
    }

    public override void Stop()
    {
        if (lobbyServerManager != null)
        {
            lobbyServerManager.RemoveFromLobbyServer();
            GameObject.Destroy(lobbyServerManager);
        }

        base.Stop();
    }

    protected override void Subscribe()
    {
        netPacketProcessor.SubscribeReusable<ServerboundClientLoginPacket, ConnectionRequest>(OnServerboundClientLoginPacket);
        netPacketProcessor.SubscribeReusable<ServerboundClientReadyPacket, NetPeer>(OnServerboundClientReadyPacket);
        netPacketProcessor.SubscribeReusable<ServerboundSaveGameDataRequestPacket, NetPeer>(OnServerboundSaveGameDataRequestPacket);
        netPacketProcessor.SubscribeReusable<ServerboundPlayerPositionPacket, NetPeer>(OnServerboundPlayerPositionPacket);
        netPacketProcessor.SubscribeReusable<ServerboundPlayerCarPacket, NetPeer>(OnServerboundPlayerCarPacket);
        netPacketProcessor.SubscribeReusable<ServerboundTimeAdvancePacket, NetPeer>(OnServerboundTimeAdvancePacket);
        netPacketProcessor.SubscribeReusable<ServerboundTrainSyncRequestPacket>(OnServerboundTrainSyncRequestPacket);
        netPacketProcessor.SubscribeReusable<ServerboundTrainDeleteRequestPacket, NetPeer>(OnServerboundTrainDeleteRequestPacket);
        netPacketProcessor.SubscribeReusable<ServerboundTrainRerailRequestPacket, NetPeer>(OnServerboundTrainRerailRequestPacket);
        netPacketProcessor.SubscribeReusable<ServerboundLicensePurchaseRequestPacket, NetPeer>(OnServerboundLicensePurchaseRequestPacket);
        netPacketProcessor.SubscribeReusable<CommonChangeJunctionPacket, NetPeer>(OnCommonChangeJunctionPacket);
        netPacketProcessor.SubscribeReusable<CommonRotateTurntablePacket, NetPeer>(OnCommonRotateTurntablePacket);
        netPacketProcessor.SubscribeReusable<CommonTrainCouplePacket, NetPeer>(OnCommonTrainCouplePacket);
        netPacketProcessor.SubscribeReusable<CommonTrainUncouplePacket, NetPeer>(OnCommonTrainUncouplePacket);
        netPacketProcessor.SubscribeReusable<CommonHoseConnectedPacket, NetPeer>(OnCommonHoseConnectedPacket);
        netPacketProcessor.SubscribeReusable<CommonHoseDisconnectedPacket, NetPeer>(OnCommonHoseDisconnectedPacket);
        netPacketProcessor.SubscribeReusable<CommonMuConnectedPacket, NetPeer>(OnCommonMuConnectedPacket);
        netPacketProcessor.SubscribeReusable<CommonMuDisconnectedPacket, NetPeer>(OnCommonMuDisconnectedPacket);
        netPacketProcessor.SubscribeReusable<CommonCockFiddlePacket, NetPeer>(OnCommonCockFiddlePacket);
        netPacketProcessor.SubscribeReusable<CommonBrakeCylinderReleasePacket, NetPeer>(OnCommonBrakeCylinderReleasePacket);
        netPacketProcessor.SubscribeReusable<CommonHandbrakePositionPacket, NetPeer>(OnCommonHandbrakePositionPacket);
        netPacketProcessor.SubscribeReusable<CommonTrainPortsPacket, NetPeer>(OnCommonTrainPortsPacket);
        netPacketProcessor.SubscribeReusable<CommonTrainFusesPacket, NetPeer>(OnCommonTrainFusesPacket);
        netPacketProcessor.SubscribeReusable<ServerboundJobTakeRequestPacket, NetPeer>(OnServerboundJobTakeRequestPacket);
        netPacketProcessor.SubscribeReusable<CommonChatPacket, NetPeer>(OnCommonChatPacket);
    }

    private void OnLoaded()
    {
        //Debug.Log($"Server loaded, isSinglePlayer: {isSinglePlayer} isPublic: {isPublic}");
        if (!isSinglePlayer && isPublic)
        {
            lobbyServerManager = NetworkLifecycle.Instance.GetOrAddComponent<LobbyServerManager>();
        }

        Log($"Server loaded, processing {joinQueue.Count} queued players");
        IsLoaded = true;
        while (joinQueue.Count > 0)
        {
            NetPeer peer = joinQueue.Dequeue();
            if (peer.ConnectionState == ConnectionState.Connected)
                OnServerboundClientReadyPacket(null, peer);
        }
    }

    public bool TryGetServerPlayer(NetPeer peer, out ServerPlayer player)
    {
        return serverPlayers.TryGetValue((byte)peer.Id, out player);
    }

    public bool TryGetNetPeer(byte id, out NetPeer peer)
    {
        return netPeers.TryGetValue(id, out peer);
    }

    #region Net Events

    public override void OnPeerConnected(NetPeer peer)
    {
    }

    public override void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        byte id = (byte)peer.Id;
        Log($"Player {(serverPlayers.TryGetValue(id, out ServerPlayer player) ? player : id)} disconnected: {disconnectInfo.Reason}");

        if (WorldStreamingInit.isLoaded)
            SaveGameManager.Instance.UpdateInternalData();

        serverPlayers.Remove(id);
        netPeers.Remove(id);
        netManager.SendToAll(WritePacket(new ClientboundPlayerDisconnectPacket
        {
            Id = id
        }), DeliveryMethod.ReliableUnordered);
    }

    public override void OnNetworkLatencyUpdate(NetPeer peer, int latency)
    {
        ClientboundPingUpdatePacket clientboundPingUpdatePacket = new()
        {
            Id = (byte)peer.Id,
            Ping = latency
        };

        SendPacketToAll(clientboundPingUpdatePacket, DeliveryMethod.ReliableUnordered, peer);

        SendPacket(peer, new ClientboundTickSyncPacket
        {
            ServerTick = NetworkLifecycle.Instance.Tick
        }, DeliveryMethod.ReliableUnordered);
    }

    public override void OnConnectionRequest(ConnectionRequest request)
    {
        netPacketProcessor.ReadAllPackets(request.Data, request);
    }

    #endregion

    #region Packet Senders

    private void SendPacketToAll<T>(T packet, DeliveryMethod deliveryMethod) where T : class, new()
    {
        NetDataWriter writer = WritePacket(packet);
        foreach (KeyValuePair<byte, NetPeer> kvp in netPeers)
            kvp.Value.Send(writer, deliveryMethod);
    }

    private void SendPacketToAll<T>(T packet, DeliveryMethod deliveryMethod, NetPeer excludePeer) where T : class, new()
    {
        NetDataWriter writer = WritePacket(packet);
        foreach (KeyValuePair<byte, NetPeer> kvp in netPeers)
        {
            if (kvp.Key == excludePeer.Id)
                continue;
            kvp.Value.Send(writer, deliveryMethod);
        }
    }

    public void SendGameParams(GameParams gameParams)
    {
        SendPacketToAll(ClientboundGameParamsPacket.FromGameParams(gameParams), DeliveryMethod.ReliableOrdered, selfPeer);
    }

    public void SendSpawnTrainCar(NetworkedTrainCar networkedTrainCar)
    {
        SendPacketToAll(ClientboundSpawnTrainCarPacket.FromTrainCar(networkedTrainCar), DeliveryMethod.ReliableOrdered, selfPeer);
    }

    public void SendDestroyTrainCar(TrainCar trainCar)
    {
        SendPacketToAll(new ClientboundDestroyTrainCarPacket
        {
            NetId = trainCar.GetNetId()
        }, DeliveryMethod.ReliableOrdered, selfPeer);
    }

    public void SendTrainsetPhysicsUpdate(ClientboundTrainsetPhysicsPacket packet, bool reliable)
    {
        SendPacketToAll(packet, reliable ? DeliveryMethod.ReliableOrdered : DeliveryMethod.Unreliable, selfPeer);
    }

    public void SendCargoState(TrainCar trainCar, ushort netId, bool isLoading, byte cargoModelIndex)
    {
        Car logicCar = trainCar.logicCar;
        CargoType cargoType = isLoading ? logicCar.CurrentCargoTypeInCar : logicCar.LastUnloadedCargoType;
        SendPacketToAll(new ClientboundCargoStatePacket
        {
            NetId = netId,
            IsLoading = isLoading,
            CargoType = (ushort)cargoType,
            CargoAmount = logicCar.LoadedCargoAmount,
            CargoModelIndex = cargoModelIndex,
            WarehouseMachineId = logicCar.CargoOriginWarehouse?.ID
        }, DeliveryMethod.ReliableOrdered, selfPeer);
    }

    public void SendCarHealthUpdate(ushort netId, float health)
    {
        SendPacketToAll(new ClientboundCarHealthUpdatePacket
        {
            NetId = netId,
            Health = health
        }, DeliveryMethod.ReliableOrdered, selfPeer);
    }

    public void SendRerailTrainCar(ushort netId, ushort rerailTrack, Vector3 worldPos, Vector3 forward)
    {
        SendPacketToAll(new ClientboundRerailTrainPacket
        {
            NetId = netId,
            TrackId = rerailTrack,
            Position = worldPos,
            Forward = forward
        }, DeliveryMethod.ReliableOrdered, selfPeer);
    }

    public void SendWindowsBroken(ushort netId, Vector3 forceDirection)
    {
        SendPacketToAll(new ClientboundWindowsBrokenPacket
        {
            NetId = netId,
            ForceDirection = forceDirection
        }, DeliveryMethod.ReliableUnordered, selfPeer);
    }

    public void SendWindowsRepaired(ushort netId)
    {
        SendPacketToAll(new ClientboundWindowsBrokenPacket
        {
            NetId = netId
        }, DeliveryMethod.ReliableUnordered, selfPeer);
    }

    public void SendMoney(float amount)
    {
        SendPacketToAll(new ClientboundMoneyPacket
        {
            Amount = amount
        }, DeliveryMethod.ReliableUnordered, selfPeer);
    }

    public void SendLicense(string id, bool isJobLicense)
    {
        SendPacketToAll(new ClientboundLicenseAcquiredPacket
        {
            Id = id,
            IsJobLicense = isJobLicense
        }, DeliveryMethod.ReliableUnordered, selfPeer);
    }

    public void SendGarage(string id)
    {
        SendPacketToAll(new ClientboundGarageUnlockPacket
        {
            Id = id
        }, DeliveryMethod.ReliableUnordered, selfPeer);
    }

    public void SendDebtStatus(bool hasDebt)
    {
        SendPacketToAll(new ClientboundDebtStatusPacket
        {
            HasDebt = hasDebt
        }, DeliveryMethod.ReliableUnordered, selfPeer);
    }

    //public void SendJobCreatePacket(NetworkedJob job)
    //{
    //    Multiplayer.Log("Sending JobCreatePacket with netId: " + job.NetId + ", Job ID: " + job.job.ID);
    //    SendPacketToAll(ClientboundJobCreatePacket.FromNetworkedJob(job),DeliveryMethod.ReliableSequenced);
    //}

    public void SendChat(string message, NetPeer exclude = null)
    {

        if (exclude != null)
        {
            NetworkLifecycle.Instance.Server.SendPacketToAll(new CommonChatPacket
            {
                message = message
            }, DeliveryMethod.ReliableUnordered, exclude);
        }
        else
        {
            NetworkLifecycle.Instance.Server.SendPacketToAll(new CommonChatPacket
            {
                message = message
            }, DeliveryMethod.ReliableUnordered);
        }
    }

    public void SendWhisper(string message, NetPeer recipient)
    {
        if(message != null || recipient != null)
        {
            NetworkLifecycle.Instance.Server.SendPacket(recipient, new CommonChatPacket
            {
                message = message
            }, DeliveryMethod.ReliableUnordered);
        }

    }

    #endregion

    #region Listeners

    private void OnServerboundClientLoginPacket(ServerboundClientLoginPacket packet, ConnectionRequest request)
    {
        // clean up username - remove leading/trailing white space, swap spaces for underscores and truncate
        packet.Username = packet.Username.Trim().Replace(' ', '_').Truncate(Settings.MAX_USERNAME_LENGTH);
        string overrideUsername = packet.Username;

        //ensure the username is unique
        int uniqueName = ServerPlayers.Where(player => player.OriginalUsername.ToLower() == packet.Username.ToLower()).Count();

        if (uniqueName > 0)
        {
            overrideUsername += uniqueName;
        }

        Guid guid;
        try
        {
            guid = new Guid(packet.Guid);
        }
        catch (ArgumentException)
        {
            // This can only happen if the sent GUID is tampered with, in which case, we aren't worried about showing a message.
            Log($"Invalid GUID from {packet.Username}{(Multiplayer.Settings.LogIps ? $" at {request.RemoteEndPoint.Address}" : "")}");
            request.Reject();
            return;
        }

        Log($"Processing login packet for {packet.Username} ({guid.ToString()}){(Multiplayer.Settings.LogIps ? $" at {request.RemoteEndPoint.Address}" : "")}");

        if (Multiplayer.Settings.Password != packet.Password)
        {
            LogWarning("Denied login due to invalid password!");
            ClientboundServerDenyPacket denyPacket = new()
            {
                ReasonKey = Locale.DISCONN_REASON__INVALID_PASSWORD_KEY
            };
            request.Reject(WritePacket(denyPacket));
            return;
        }

        if (packet.BuildMajorVersion != BuildInfo.BUILD_VERSION_MAJOR)
        {
            LogWarning($"Denied login to incorrect game version! Got: {packet.BuildMajorVersion}, expected: {BuildInfo.BUILD_VERSION_MAJOR}");
            ClientboundServerDenyPacket denyPacket = new()
            {
                ReasonKey = Locale.DISCONN_REASON__GAME_VERSION_KEY,
                ReasonArgs = new[] { BuildInfo.BUILD_VERSION_MAJOR.ToString(), packet.BuildMajorVersion.ToString() }
            };
            request.Reject(WritePacket(denyPacket));
            return;
        }

        if (netManager.ConnectedPeersCount >= Multiplayer.Settings.MaxPlayers || isSinglePlayer && netManager.ConnectedPeersCount >= 1)
        {
            LogWarning("Denied login due to server being full!");
            ClientboundServerDenyPacket denyPacket = new()
            {
                ReasonKey = Locale.DISCONN_REASON__FULL_SERVER_KEY
            };
            request.Reject(WritePacket(denyPacket));
            return;
        }

        ModInfo[] clientMods = packet.Mods;
        if (!serverMods.SequenceEqual(clientMods))
        {
            ModInfo[] missing = serverMods.Except(clientMods).ToArray();
            ModInfo[] extra = clientMods.Except(serverMods).ToArray();
            LogWarning($"Denied login due to mod mismatch! {missing.Length} missing, {extra.Length} extra");
            ClientboundServerDenyPacket denyPacket = new()
            {
                ReasonKey = Locale.DISCONN_REASON__MODS_KEY,
                Missing = missing,
                Extra = extra
            };
            request.Reject(WritePacket(denyPacket));
            return;
        }

        NetPeer peer = request.Accept();

        ServerPlayer serverPlayer = new()
        {
            Id = (byte)peer.Id,
            Username = overrideUsername,
            OriginalUsername = packet.Username,
            Guid = guid
        };

        serverPlayers.Add(serverPlayer.Id, serverPlayer);
    }

    private void OnServerboundSaveGameDataRequestPacket(ServerboundSaveGameDataRequestPacket packet, NetPeer peer)
    {
        if (netPeers.ContainsKey((byte)peer.Id))
        {
            LogWarning("Denied save game data request from already connected peer!");
            return;
        }

        TryGetServerPlayer(peer, out ServerPlayer player);

        SendPacket(peer, ClientboundGameParamsPacket.FromGameParams(Globals.G.GameParams), DeliveryMethod.ReliableOrdered);
        SendPacket(peer, ClientboundSaveGameDataPacket.CreatePacket(player), DeliveryMethod.ReliableOrdered);
    }

    private void OnServerboundClientReadyPacket(ServerboundClientReadyPacket packet, NetPeer peer)
    {
        byte peerId = (byte)peer.Id;

        // Allow clients to connect before the server is fully loaded
        if (!IsLoaded)
        {
            joinQueue.Enqueue(peer);
            SendPacket(peer, new ClientboundServerLoadingPacket(), DeliveryMethod.ReliableOrdered);
            return;
        }

        // Unpause physics
        if (AppUtil.Instance.IsTimePaused)
            AppUtil.Instance.RequestSystemOnValueChanged(0.0f);

        // Allow the player to receive packets
        netPeers.Add(peerId, peer);

        // Send the new player to all other players
        ServerPlayer serverPlayer = serverPlayers[peerId];
        ClientboundPlayerJoinedPacket clientboundPlayerJoinedPacket = new()
        {
            Id = peerId,
            Username = serverPlayer.Username,
            Guid = serverPlayer.Guid.ToByteArray()
        };
        SendPacketToAll(clientboundPlayerJoinedPacket, DeliveryMethod.ReliableOrdered, peer);

        ChatManager.ServerMessage(serverPlayer.Username + " joined the game", null, peer);

        Log($"Client {peer.Id} is ready. Sending world state");

        // No need to sync the world state if the player is the host
        if (NetworkLifecycle.Instance.IsHost(peer))
        {
            SendPacket(peer, new ClientboundRemoveLoadingScreenPacket(), DeliveryMethod.ReliableOrdered);
            return;
        }

        SendPacket(peer, new ClientboundBeginWorldSyncPacket(), DeliveryMethod.ReliableOrdered);

        // Send weather state
        SendPacket(peer, WeatherDriver.Instance.GetSaveData().ToObject<ClientboundWeatherPacket>(), DeliveryMethod.ReliableOrdered);

        // Send junctions and turntables
        SendPacket(peer, new ClientboundRailwayStatePacket
        {
            SelectedJunctionBranches = NetworkedJunction.IndexedJunctions.Select(j => (byte)j.Junction.selectedBranch).ToArray(),
            TurntableRotations = NetworkedTurntable.IndexedTurntables.Select(j => j.TurntableRailTrack.currentYRotation).ToArray()
        }, DeliveryMethod.ReliableOrdered);

        // Send trains
        foreach (Trainset set in Trainset.allSets)
        {
            LogDebug(() => $"Sending trainset {set.firstCar.GetNetId()} with {set.cars.Count} cars");
            SendPacket(peer, ClientboundSpawnTrainSetPacket.FromTrainSet(set), DeliveryMethod.ReliableOrdered);
        }

        /* Temp for stable release
        //send jobs - do we need a job manager/job IDs to make this easier?
        foreach(StationController station in StationController.allStations)
        {
            List<JobData> jobData = new List<JobData>();
            List<ushort> netIds = new List<ushort>();

            foreach(Job job in station.logicStation.availableJobs)
            {
                jobData.Add(JobData.FromJob(job));
                netIds.Add(NetworkedJob.GetFromJob(job).NetId);
            }

            SendPacket(peer,
                        new ClientboundJobsPacket
                            {
                                stationId = station.logicStation.ID,
                                netIds = netIds.ToArray(),
                                Jobs = jobData.ToArray(),
                            },
                        DeliveryMethod.ReliableOrdered
                    );
                
        }*/


        // Send existing players
        foreach (ServerPlayer player in ServerPlayers)
        {
            if (player.Id == peer.Id)
                continue;
            SendPacket(peer, new ClientboundPlayerJoinedPacket
            {
                Id = player.Id,
                Username = player.Username,
                Guid = player.Guid.ToByteArray(),
                TrainCar = player.CarId,
                Position = player.RawPosition,
                Rotation = player.RawRotationY
            }, DeliveryMethod.ReliableOrdered);
        }

        // All data has been sent, allow the client to load into the world.
        SendPacket(peer, new ClientboundRemoveLoadingScreenPacket(), DeliveryMethod.ReliableOrdered);

        serverPlayer.IsLoaded = true;
    }

    private void OnServerboundPlayerPositionPacket(ServerboundPlayerPositionPacket packet, NetPeer peer)
    {
        if (TryGetServerPlayer(peer, out ServerPlayer player))
        {
            player.RawPosition = packet.Position;
            player.RawRotationY = packet.RotationY;
        }

        ClientboundPlayerPositionPacket clientboundPacket = new()
        {
            Id = (byte)peer.Id,
            Position = packet.Position,
            MoveDir = packet.MoveDir,
            RotationY = packet.RotationY,
            IsJumpingIsOnCar = packet.IsJumpingIsOnCar
        };

        SendPacketToAll(clientboundPacket, DeliveryMethod.Sequenced, peer);
    }

    private void OnServerboundPlayerCarPacket(ServerboundPlayerCarPacket packet, NetPeer peer)
    {
        if (packet.CarId != 0 && !NetworkedTrainCar.Get(packet.CarId, out NetworkedTrainCar _))
            return;

        if (TryGetServerPlayer(peer, out ServerPlayer player))
            player.CarId = packet.CarId;

        ClientboundPlayerCarPacket clientboundPacket = new()
        {
            Id = (byte)peer.Id,
            CarId = packet.CarId
        };

        SendPacketToAll(clientboundPacket, DeliveryMethod.ReliableOrdered, peer);
    }

    private void OnServerboundTimeAdvancePacket(ServerboundTimeAdvancePacket packet, NetPeer peer)
    {
        SendPacketToAll(new ClientboundTimeAdvancePacket
        {
            amountOfTimeToSkipInSeconds = packet.amountOfTimeToSkipInSeconds
        }, DeliveryMethod.ReliableUnordered, peer);
    }

    private void OnCommonChangeJunctionPacket(CommonChangeJunctionPacket packet, NetPeer peer)
    {
        SendPacketToAll(packet, DeliveryMethod.ReliableUnordered, peer);
    }

    private void OnCommonRotateTurntablePacket(CommonRotateTurntablePacket packet, NetPeer peer)
    {
        SendPacketToAll(packet, DeliveryMethod.ReliableOrdered, peer);
    }

    private void OnCommonTrainCouplePacket(CommonTrainCouplePacket packet, NetPeer peer)
    {
        SendPacketToAll(packet, DeliveryMethod.ReliableUnordered, peer);
    }

    private void OnCommonTrainUncouplePacket(CommonTrainUncouplePacket packet, NetPeer peer)
    {
        SendPacketToAll(packet, DeliveryMethod.ReliableUnordered, peer);
    }

    private void OnCommonHoseConnectedPacket(CommonHoseConnectedPacket packet, NetPeer peer)
    {
        SendPacketToAll(packet, DeliveryMethod.ReliableUnordered, peer);
    }

    private void OnCommonHoseDisconnectedPacket(CommonHoseDisconnectedPacket packet, NetPeer peer)
    {
        SendPacketToAll(packet, DeliveryMethod.ReliableUnordered, peer);
    }

    private void OnCommonMuConnectedPacket(CommonMuConnectedPacket packet, NetPeer peer)
    {
        SendPacketToAll(packet, DeliveryMethod.ReliableUnordered, peer);
    }

    private void OnCommonMuDisconnectedPacket(CommonMuDisconnectedPacket packet, NetPeer peer)
    {
        SendPacketToAll(packet, DeliveryMethod.ReliableUnordered, peer);
    }

    private void OnCommonCockFiddlePacket(CommonCockFiddlePacket packet, NetPeer peer)
    {
        SendPacketToAll(packet, DeliveryMethod.ReliableUnordered, peer);
    }

    private void OnCommonBrakeCylinderReleasePacket(CommonBrakeCylinderReleasePacket packet, NetPeer peer)
    {
        SendPacketToAll(packet, DeliveryMethod.ReliableUnordered, peer);
    }

    private void OnCommonHandbrakePositionPacket(CommonHandbrakePositionPacket packet, NetPeer peer)
    {
        SendPacketToAll(packet, DeliveryMethod.ReliableOrdered, peer);
    }

    private void OnCommonTrainPortsPacket(CommonTrainPortsPacket packet, NetPeer peer)
    {
        if (!TryGetServerPlayer(peer, out ServerPlayer player))
            return;
        if (!NetworkedTrainCar.Get(packet.NetId, out NetworkedTrainCar networkedTrainCar))
            return;
        if (!NetworkLifecycle.Instance.IsHost(peer) && !networkedTrainCar.Server_ValidateClientSimFlowPacket(player, packet))
            return;

        SendPacketToAll(packet, DeliveryMethod.ReliableOrdered, peer);
    }

    private void OnCommonTrainFusesPacket(CommonTrainFusesPacket packet, NetPeer peer)
    {
        SendPacketToAll(packet, DeliveryMethod.ReliableOrdered, peer);
    }

    private void OnServerboundTrainSyncRequestPacket(ServerboundTrainSyncRequestPacket packet)
    {
        if (NetworkedTrainCar.Get(packet.NetId, out NetworkedTrainCar networkedTrainCar))
            networkedTrainCar.Server_DirtyAllState();
    }

    private void OnServerboundTrainDeleteRequestPacket(ServerboundTrainDeleteRequestPacket packet, NetPeer peer)
    {
        if (!TryGetServerPlayer(peer, out ServerPlayer player))
            return;
        if (!NetworkedTrainCar.Get(packet.NetId, out NetworkedTrainCar networkedTrainCar))
            return;

        if (networkedTrainCar.HasPlayers)
        {
            LogWarning($"{player.Username} tried to delete a train with players in it!");
            return;
        }

        TrainCar trainCar = networkedTrainCar.TrainCar;
        float cost = trainCar.playerSpawnedCar ? 0.0f : Mathf.RoundToInt(Globals.G.GameParams.DeleteCarMaxPrice);
        if (!Inventory.Instance.RemoveMoney(cost))
        {
            LogWarning($"{player.Username} tried to delete a train without enough money to do so!");
            return;
        }

        Job job = JobsManager.Instance.GetJobOfCar(trainCar);
        switch (job?.State)
        {
            case JobState.Available:
                job.ExpireJob();
                break;
            case JobState.InProgress:
                JobsManager.Instance.AbandonJob(job);
                break;
        }

        CarSpawner.Instance.DeleteCar(trainCar);
    }

    private void OnServerboundTrainRerailRequestPacket(ServerboundTrainRerailRequestPacket packet, NetPeer peer)
    {
        if (!TryGetServerPlayer(peer, out ServerPlayer player))
            return;
        if (!NetworkedTrainCar.Get(packet.NetId, out NetworkedTrainCar networkedTrainCar))
            return;
        if (!NetworkedRailTrack.Get(packet.TrackId, out NetworkedRailTrack networkedRailTrack))
            return;

        TrainCar trainCar = networkedTrainCar.TrainCar;
        Vector3 position = packet.Position + WorldMover.currentMove;
        float cost = RerailController.CalculatePrice((networkedTrainCar.transform.position - position).magnitude, trainCar.carType, Globals.G.GameParams.RerailMaxPrice);
        if (!Inventory.Instance.RemoveMoney(cost))
        {
            LogWarning($"{player.Username} tried to rerail a train without enough money to do so!");
            return;
        }

        trainCar.Rerail(networkedRailTrack.RailTrack, position, packet.Forward);
    }

    private void OnServerboundLicensePurchaseRequestPacket(ServerboundLicensePurchaseRequestPacket packet, NetPeer peer)
    {
        if (!TryGetServerPlayer(peer, out ServerPlayer player))
            return;

        JobLicenseType_v2 jobLicense = null;
        GeneralLicenseType_v2 generalLicense = null;
        float? price = packet.IsJobLicense
            ? (jobLicense = Globals.G.Types.jobLicenses.Find(l => l.id == packet.Id))?.price
            : (generalLicense = Globals.G.Types.generalLicenses.Find(l => l.id == packet.Id))?.price;

        if (!price.HasValue)
        {
            LogWarning($"{player.Username} tried to purchase an invalid {(packet.IsJobLicense ? "job" : "general")} license with id {packet.Id}!");
            return;
        }

        CareerManagerDebtController.Instance.RefreshExistingDebtsState();
        if (CareerManagerDebtController.Instance.NumberOfNonZeroPricedDebts > 0)
        {
            LogWarning($"{player.Username} tried to purchase a {(packet.IsJobLicense ? "job" : "general")} license with id {packet.Id} while having existing debts!");
            return;
        }

        if (!Inventory.Instance.RemoveMoney(price.Value))
        {
            LogWarning($"{player.Username} tried to purchase a {(packet.IsJobLicense ? "job" : "general")} license with id {packet.Id} without enough money to do so!");
            return;
        }

        if (packet.IsJobLicense)
            LicenseManager.Instance.AcquireJobLicense(jobLicense);
        else
            LicenseManager.Instance.AcquireGeneralLicense(generalLicense);
    }


    private void OnServerboundJobTakeRequestPacket(ServerboundJobTakeRequestPacket packet, NetPeer peer)
    {
        /* Temp for stable release
        NetworkedJob networkedJob;

        if (!NetworkedJob.Get(packet.netId, out networkedJob))
        {
            Multiplayer.Log($"OnServerboundJobTakeRequestPacket netId Not Found: {packet.netId}");
            return;
        }

        if (networkedJob.job.State != JobState.Available) {

            Multiplayer.Log($"OnServerboundJobTakeRequestPacket jobId: {networkedJob.job.ID}, DENIED");
            ServerPlayer player = ServerPlayers.First(x => x.Guid == networkedJob.takenBy);
            //deny the request
            SendPacket(peer, new ClientboundJobTakeResponsePacket { netId = packet.netId, granted = false, playerId = player.Id }, DeliveryMethod.ReliableOrdered);
        }
        else
        {
            //probably need to do more here
            ServerPlayer player;
            if (!TryGetServerPlayer(peer, out player))
                return;

            networkedJob.takenBy = player.Guid;
            //networkedJob.job.State = JobState.InProgress;

            //todo: officially take the job
            Multiplayer.Log($"OnServerboundJobTakeRequestPacket jobId: {networkedJob.job.ID}, GRANTED");
            SendPacket(peer, new ClientboundJobTakeResponsePacket { netId = packet.netId, granted = true, playerId = player.Id }, DeliveryMethod.ReliableOrdered);

        }
        */
    }

    private void OnCommonChatPacket(CommonChatPacket packet, NetPeer peer)
    {
        ChatManager.ProcessMessage(packet.message,peer);
    }
    #endregion
}
