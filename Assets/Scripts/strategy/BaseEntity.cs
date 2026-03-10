using System;
using System.Collections.Generic;
using UnityEngine;


public class BaseEntity : MonoBehaviour
{
    [Header("Identity")] [SerializeField] private string id;
    [SerializeField] private string displayName = "Entity";
    private EntityType entityType = EntityType.Pnj;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float speed = 2.5f;
    [SerializeField] private MovementStrategy movementStrategy;

    [Header("Runtime State (read-only)")]
    [SerializeField] private Direction desiredDirection = Direction.Still;
    [SerializeField] private State state = State.Idle;
    private Direction _lastNonStillDir = Direction.Right;

    [Header("Sensing (Player Overlap)")]
    [Tooltip("Rayon du capteur joueur (m).")]
    [SerializeField, Min(0f)] private float playerSenseRadius = 2.0f;
    [Tooltip("Layers à considérer comme Joueur (ex: 'Player').")]
    [SerializeField] private LayerMask playerMask = ~0;
    [SerializeField] private bool debugSensing = true;

    private Rigidbody _rb;
    private EntityRole _role;
    
    private readonly HashSet<int> _playersInside = new HashSet<int>();

    public string ID => id;
    public string DisplayName => displayName;
    public EntityType Type => entityType;
    public float Speed => speed;
    public Direction DesiredDirection => desiredDirection;
    public State CurrentState => state;
    public MovementStrategy Strategy => movementStrategy;
    public EntityRole Role => _role;

    public void SetDisplayName(string name) => displayName = name;
    public void SetID(string newId) => id = newId;
    public void SetType(EntityType t) => entityType = t;
    public void SetSpeed(float newSpeed) => speed = Mathf.Max(0f, newSpeed);
    public void SetStrategy(MovementStrategy strategy) => movementStrategy = strategy;

    public void SetDesired(Direction dir, State sIfMoving)
    {
        if (dir == Direction.Still)
        {
            state = State.Idle;
            return;
        }
        
        desiredDirection = dir;
        _lastNonStillDir = dir;
        state = sIfMoving;
    }
    public void SetState(State s) => state = s;

    public void StopMoving()
    {
        state = State.Idle;
    }
    private void Reset()
    {
        playerMask = LayerMask.GetMask("Player"); 
    }

    private void OnValidate()
    {
        var roles = GetComponents<EntityRole>();
        if (roles.Length > 1)
        {
            Debug.LogWarning($"[BaseEntity] {name}: Plusieurs rôles trouvés ({roles.Length}). Garde seulement un script de rôle par entité.");
        }
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.constraints = RigidbodyConstraints.FreezeRotation; // rotation fige

        _role = GetComponent<EntityRole>();
        if (_role != null)
        {
            _role.Initialize(this);
            SetType(_role.RoleType);
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        
        if (movementStrategy != null)
            movementStrategy.Tick(this, dt);


        if (_role != null)
            _role.Tick(dt);
        
        SensePlayers();
    }

    public void MoveInDirection(Direction d, float moveSpeed, float dt)
    {
        Vector3 dir = DirectionUtil.ToVector(d);
        Vector3 delta = dir * moveSpeed * dt;
        _rb.MovePosition(_rb.position + delta);
    }
    
    private void SensePlayers()
    {
        if (playerSenseRadius <= 0f) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, playerSenseRadius, playerMask, QueryTriggerInteraction.Collide);
        
        var current = new HashSet<int>();
        foreach (var c in hits)
        {
            if (!c) continue;
            int id = c.GetInstanceID();
            current.Add(id);

            // entré a voir ?
            if (!_playersInside.Contains(id))
            {
                _playersInside.Add(id);
                if (debugSensing)
                    Debug.Log($"[{displayName}] {entityType} - Player ENTER: {c.name}");
            }
        }
        
        var toRemove = new List<int>();
        foreach (var id in _playersInside)
        {
            if (!current.Contains(id))
            {
                toRemove.Add(id);
                if (debugSensing)
                    Debug.Log($"[{displayName}] {entityType} - Player EXIT: #{id}");
            }
        }
        foreach (var id in toRemove)
            _playersInside.Remove(id);
    }

    private void OnDrawGizmosSelected()
    {
        if (playerSenseRadius > 0f)
        {
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.2f);
            Gizmos.DrawSphere(transform.position, playerSenseRadius);
        }
    }
}
