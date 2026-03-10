using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement))]
public class PlayerStatusController : NetworkBehaviour
{
    [Header("Duration")]
    [SerializeField] private float poisonDuration = 5f;
    [SerializeField] private float sleepyDuration = 1.25f;
    [SerializeField] private float hemorrhageDuration = 5f;
    [SerializeField] private float weaknessDuration = 4f;
    [SerializeField] private float slownessDuration = 4f;

    [Header("Configuration")]
    [SerializeField] private float dotTickInterval = 1f;
    [SerializeField] private int poisonDamagePerTick = 1;
    [SerializeField] private int hemorrhageDamagePerTick = 1;
    [SerializeField, Range(0.1f, 1f)] private float slownessSpeedMult = 0.6f;
    [SerializeField] private bool allowOverrideState = false;

    private readonly NetworkVariable<int> _stateType = new NetworkVariable<int>((int)PlayerStatusType.Normal, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<float> _stateSpeedMult = new NetworkVariable<float>(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<float> _stateOutgoingDamageMult = new NetworkVariable<float>(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<bool> _stateCanMove = new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public PlayerStatusType CurrentType => (PlayerStatusType)_stateType.Value;
    public float SpeedMultiplier => _stateSpeedMult.Value;
    public float OutgoingDamageMultiplier => _stateOutgoingDamageMult.Value;
    public bool CanMove => _stateCanMove.Value;

    private PlayerMovement _pm;
    private PlayerState _current = new NormalState();

    private void Awake()
    {
        _pm = GetComponent<PlayerMovement>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            SetState(new NormalState());
    }

    private void Update()
    {
        if (!IsServer) return;
        _current?.Tick(this, Time.deltaTime);
    }

    public void TryApplyRandomStateFromAttacker(int attackerPlayerId)
    {
        if (!IsServer) return;

        if (!allowOverrideState && CurrentType != PlayerStatusType.Normal)
            return;

        var pmgr = PlayerManager.Instance;
        if (pmgr == null) return;

        var chances = pmgr.GetStatusChances(attackerPlayerId);
        float pPoison = Mathf.Clamp(chances.poison, 0f, 100f);
        float pSleepy = Mathf.Clamp(chances.sleepy, 0f, 100f);
        float pHem    = Mathf.Clamp(chances.hemorrhage, 0f, 100f);
        float pWeak   = Mathf.Clamp(chances.weakness, 0f, 100f);
        float pSlow   = Mathf.Clamp(chances.slowness, 0f, 100f);

        float total = pPoison + pSleepy + pHem + pWeak + pSlow;
        if (total <= 0.001f) return;

        float r = Random.Range(0f, total);

        if ((r -= pPoison) < 0f)
        {
            SetState(new PoisonState(poisonDuration, dotTickInterval, poisonDamagePerTick));
            return;
        }
        if ((r -= pSleepy) < 0f)
        {
            SetState(new SleepyState(sleepyDuration));
            return;
        }
        if ((r -= pHem) < 0f)
        {
            SetState(new HemorrhageState(hemorrhageDuration, dotTickInterval, hemorrhageDamagePerTick));
            return;
        }
        if ((r -= pWeak) < 0f)
        {
            SetState(new WeaknessState(weaknessDuration));
            return;
        }

        SetState(new SlownessState(slownessDuration, slownessSpeedMult));
    }

    public void SetState(PlayerState next)
    {
        if (!IsServer) return;
        if (next == null) next = new NormalState();
        _current?.Exit(this);
        _current = next;
        _current.Enter(this);
        _stateType.Value = (int)_current.Type;
        _stateCanMove.Value = _current.CanMove;
        _stateSpeedMult.Value = Mathf.Clamp(_current.SpeedMultiplier, 0.05f, 10f);
        _stateOutgoingDamageMult.Value = Mathf.Clamp(_current.OutgoingDamageMultiplier, 0f, 10f);
    }


    public void ApplyDotDamage(int amount, bool leaveAtLeastOne)
    {
        if (!IsServer || _pm == null) return;
        amount = Mathf.Max(0, amount);
        if (amount == 0) return;

        int cur = _pm.currentHealth.Value;
        int next = cur - amount;

        if (leaveAtLeastOne) next = Mathf.Max(1, next);
        else next = Mathf.Max(0, next);

        _pm.currentHealth.Value = next;

        if (_pm.currentHealth.Value <= 0)
        {
            _pm.OnDeathClientRpc_Expose();
        }
    }
}
