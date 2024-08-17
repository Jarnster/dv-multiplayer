using Multiplayer.Networking.Data;
using System;
using UnityEngine;

namespace Multiplayer.Components.Networking.World;

public class NetworkedRigidbody : TickedQueue<RigidbodySnapshot>
{
    private Rigidbody rigidbody;

    protected override void OnEnable()
    {
        rigidbody = GetComponent<Rigidbody>();
        if (rigidbody == null)
        {
            Multiplayer.LogError($"{gameObject.name}: {nameof(NetworkedRigidbody)} requires a {nameof(Rigidbody)} component on the same GameObject!");
            return;
        }

        base.OnEnable();
    }

    protected override void Process(RigidbodySnapshot snapshot, uint snapshotTick)
    {
        if (snapshot == null)
        {
            Multiplayer.LogError($"NetworkedRigidBody.Process() Snapshot NULL!");
            return;
        }

        try
        {
            Multiplayer.LogDebug(()=>$"NetworkedRigidBody.Process() {snapshot.IncludedDataFlags}, {snapshot.Position.ToString() ?? "null"}, {snapshot.Rotation.ToString() ?? "null"}, {snapshot.Velocity.ToString() ?? "null"}, {snapshot.AngularVelocity.ToString() ?? "null"}");
            snapshot.Apply(rigidbody);
        }
        catch (Exception ex) 
        {
            Multiplayer.LogError($"NetworkedRigidBody.Process() {ex.Message}\r\n {ex.StackTrace}");
        }
    }
}
