using System.Collections;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class ProjectilPlayer : NetworkBehaviour
{
    [Header("Params (peuvent être surchargés via Setup avant Spawn)")]
    [SerializeField] private Vector2 directionXZ = Vector2.right;
    [SerializeField] private float speed = 10f;
    [SerializeField] private float speedMult = 1f;
    [SerializeField] private float lifeTime = 2f;
    [SerializeField] private bool alignToVelocity = true;


    /*[SerializeField] private bool logOnServer = false;*/

    public NetworkVariable<int> OwnerPlayerId = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Rigidbody _rb;
    private Vector3 _velocity;
    private bool _useRb;
    private bool _setupDoneOnServer;


    public void ServerSetupBeforeSpawn(Vector2 dirXZ, float baseSpeed, float mult, float lifeSeconds, int ownerId)
    {
        directionXZ = dirXZ;
        speed       = baseSpeed;
        speedMult   = mult;
        lifeTime    = lifeSeconds;
        OwnerPlayerId.Value = ownerId;        
        _setupDoneOnServer   = true;
        ApplyIdentityTag(ownerId);
    }
  

    private void Awake()
    {
        _rb    = GetComponent<Rigidbody>();
        _useRb = _rb != null && !_rb.isKinematic;
    }

    public override void OnNetworkSpawn()
    {
        ApplyIdentityTag(OwnerPlayerId.Value);
        OwnerPlayerId.OnValueChanged += (_, v) => ApplyIdentityTag(v);

        if (IsServer)
        {
            if (lifeTime > 0f) StartCoroutine(LifeCoroutine(lifeTime));
        }
    }

    private void Start()
    {
        if (!IsServer) return;
        
        Vector2 dir = directionXZ.sqrMagnitude > 1e-6f ? directionXZ.normalized : new Vector2(transform.forward.x, transform.forward.z).normalized;

        float finalSpeed = Mathf.Max(0f, speed * speedMult);
        _velocity = new Vector3(dir.x, 0f, dir.y) * finalSpeed;

        if (alignToVelocity && _velocity.sqrMagnitude > 1e-6f)
        {
            transform.rotation = Quaternion.LookRotation(_velocity, Vector3.up);
        }

        if (_useRb) _rb.linearVelocity = _velocity;

        /*if (logOnServer)
            Debug.Log($"[ProjectilPlayer] Server start vel={_velocity} owner={OwnerPlayerId.Value}", this);*/
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;
        if (!_useRb)
            transform.position += _velocity * Time.fixedDeltaTime;
    }

    private IEnumerator LifeCoroutine(float t)
    {
        yield return new WaitForSeconds(t);
        if (NetworkObject && NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
        else
            Destroy(gameObject);
    }

    private void OnDestroy()
    {
        OwnerPlayerId.OnValueChanged -= (_,_) => { };
    }

    private void ApplyIdentityTag(int ownerId)
    {
        string t = ownerId == 1 ? "ProjectilePlayer1" : ownerId == 2 ? "ProjectilePlayer2" : "Untagged";
        
        foreach (var tr in GetComponentsInChildren<Transform>(true))
            tr.gameObject.tag = t;
    }
}
