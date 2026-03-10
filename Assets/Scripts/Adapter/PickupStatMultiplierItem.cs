using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PickupStatMultiplierItem : NetworkBehaviour
{
    [Header("Effect")]
    public float mult = 1.25f;

    [Header("Destroy")]
    public bool despawnNetworkObject = true;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        var pm = other.GetComponentInParent<PlayerMovement>();
        if (pm == null) return;

        IPlayerStatsTarget target = new PlayerStatsAdapter(pm);

        target.ApplyGlobalStatMultiplier(mult);
        target.ApplyConditionalSpeed();

        if (despawnNetworkObject && TryGetComponent(out NetworkObject no) && no.IsSpawned)
        {
            no.Despawn(true);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}