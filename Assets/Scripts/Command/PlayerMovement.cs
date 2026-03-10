using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Player Id")]
    [Range(0,4)]public int currentId = 0;
    
    [Header("Référence")]
    public EquipmentManager equipementManager;
    public Inventory inventory;
    public GameInventory gameInventory;
    public RadialCursor8Way radialCursor;
    private Rigidbody rb;

    [Header("parametre stat")]
    public NetworkVariable<int> currentHealth = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    [SerializeField] private NetworkVariable<float> externalSpeedMult = new NetworkVariable<float>(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public int maxHealth = 10;

    [SerializeField] private PlayerStatusController statusController;

    [SerializeField] private float knockbackUntil;

    [SerializeField] private bool inputLocked;
    
    [Header("Parametre des mouvement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float runMult = 2f;
    [SerializeField] private float runRampUpTime = 0.25f;   
    [SerializeField] private float runRampDownTime = 0.20f; 
    private float _runMultCurrent = 1f;  
    private Coroutine _runLerpRoutine;
    [SerializeField]private float midAirMoveSpeed = 3f;       
    private float inputDeadzone = 0.2f;
    [HideInInspector] public bool isMenuOn;

    [Header("Parametre mode Revert")]
    [SerializeField] private float recordWindowSeconds = 10f;

    [Header("Punch")]
    [SerializeField]private float CoolDown = 0.5f;
    [SerializeField]private float punchLogInterval = 0.2f;
    private bool isPunching = false;
    private float punchTimer = 0f;
    private float punchLogTimer = 0f;
    [SerializeField]private float PunchDistance = 3f;
    [SerializeField]private float punchEaseStart = 0.75f;
    private float _punchLastS;
    private Coroutine punchRoutine; 
    
    [Header("Gravity (custom RB)")]
    public float globalGravity = -9.81f;
    public float onGroundGravity = 1.0f;
    public float elevationSpeed = 0.6f;
    public float fallSpeed = 1.8f;
    public float fallTerminalReference = 20f;

    [Header("Jump")]
    public float Force = 7f;
    private float lastJumpInitVelY = 6f;
    [SerializeField] private float inputOnThreshold  = 0.20f; 
    [SerializeField] private float inputOffThreshold = 0.15f; 
    [SerializeField] private float airborneStickGrace = 0.08f;
    private bool   _moveLatched;            
    private bool   _sentMoving;             
    private Vector2 _lastNonZeroInput;      
    private float  _noInputTimer;

    [Header("Ground Check (capsule)")]
    [SerializeField] private LayerMask groundLayer;
    private Vector3 onGroundZoneCenterLocal = new Vector3(0f, -0.9f, 0f);
    private float onGroundZoneHeight = 0.5f;
    private float onGroundZoneRadius = 0.3f;
    
    [Header("Shoot / Projectile")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float defaultProjectileSpeed = 12f;
    [SerializeField] private float defaultProjectileLifeTime = 2f;
    [SerializeField] private float shootCoolDown = 1f;
    
    private bool isGrounded;
    private bool wasGrounded;
    private bool isMidAirJump; 
    private bool isMidAir;     

    
    
    [Header("Input action")]
    [SerializeField] private string actionMapName = "Gameplay";
    [SerializeField] private string moveActionName = "Move";
    [SerializeField] private string revertActionName = "Revert";
    [SerializeField] private string runActionName = "Run";
    [SerializeField] private string jumpActionName = "Jump";
    [SerializeField] private string punchActionName = "Punch";
    [SerializeField] private string shootActionName = "Shoot";
    [SerializeField] private string reloadActionName = "Reload";
    [SerializeField] private string unloadActionName = "Unload";
    [SerializeField] private string pauseActionName = "Pause";

    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction revertAction;
    private InputAction runAction;
    private InputAction jumpAction;
    private InputAction punchAction;
    private InputAction shootAction;
    private InputAction reloadAction;
    private InputAction unloadAction;
    private InputAction pauseAction;

    private Coroutine revertRoutine;
    private bool revertHeld = false;
    private bool isReverting = false;

    private Material mat;

    private struct MovementRecord
    {
        public IMovement commandRecorder;
        public float deltatimeRecorded;
        public MovementRecord(IMovement command, float deltatime) { commandRecorder = command; deltatimeRecorded = deltatime; }
    }
    private readonly List<MovementRecord> historique = new List<MovementRecord>();
    private float movementWhileTime = 0f;

    
    private void Awake()
    {
        currentId = PlayerManager.Instance.GetNextPlayerId(currentId);
        PlayerManager.Instance.BindEquipmentManagersByPlayerID(equipementManager, currentId);
        PlayerManager.Instance.AssignPlayerRef(gameObject, currentId);
        playerInput = GetComponent<PlayerInput>();
        rb = GetComponent<Rigidbody>();
        radialCursor = GetComponent<RadialCursor8Way>();
        PlayerManager.Instance.GetRadialCursor8Way(radialCursor);
        statusController = GetComponent<PlayerStatusController>();
        mat = GetComponent<SpriteRenderer>().material;
    }
    
    public override void OnNetworkSpawn()
    {
        if (IsServer && currentHealth.Value <= 0)
            currentHealth.Value = maxHealth;
        ColorClientRpc();
    }

    private void OnEnable()
    {
        if (rb != null) rb.useGravity = false;

        var actions = playerInput.actions;
        if (!string.IsNullOrEmpty(actionMapName))
            actions.FindActionMap(actionMapName, throwIfNotFound: true);

        moveAction = actions[moveActionName];
        revertAction = actions[revertActionName];
        runAction = actions[runActionName];
        jumpAction = actions[jumpActionName];
        punchAction  = actions[punchActionName];
        shootAction = actions[shootActionName];
        reloadAction = actions[reloadActionName];
        unloadAction = actions[unloadActionName];
        pauseAction = actions[pauseActionName];
        
        

        revertAction.performed += OnRevertPressed;
        revertAction.canceled  += OnRevertReleased;
        runAction.performed += OnRunPressed;
        runAction.canceled += OnRunReleased;
        jumpAction.performed += OnJumpPressed;
        jumpAction.canceled += OnJumpReleased;
        punchAction.performed  += OnPunchPressed;
        shootAction.performed += OnShootPressed;
        reloadAction.performed += OnReloadPressed;
        unloadAction.performed += OnUnloadPressed;
        pauseAction.performed += OnPausePressed;

        moveAction.Enable(); 
        revertAction.Enable(); 
        runAction.Enable(); 
        jumpAction.Enable(); 
        punchAction.Enable();
        shootAction.Enable();
        reloadAction.Enable();
        unloadAction.Enable();
        pauseAction.Enable();
    }

    private void OnDisable()
    {
        if (revertAction != null)
        {
            revertAction.performed -= OnRevertPressed; 
            revertAction.canceled -= OnRevertReleased;
        }

        if (runAction != null)
        {
            runAction.performed -= OnRunPressed;
            runAction.canceled -= OnRunReleased;
        }

        if (jumpAction != null)
        {
            jumpAction.performed -= OnJumpPressed;   
            jumpAction.canceled -= OnJumpReleased;
        }

        if (punchAction != null)
        {
            punchAction.performed -= OnPunchPressed;
        }
        
        if (shootAction != null)
        {
            shootAction.performed -= OnShootPressed;
        }

        if (reloadAction != null)
        {
            reloadAction.performed -= OnReloadPressed;
        }

        if (unloadAction != null)
        {
            unloadAction.performed -= OnUnloadPressed;
        }
        
        if (pauseAction != null)
        {
            pauseAction.performed -= OnPausePressed;
        }
        
        
    }

    private void FixedUpdate()
    {
        wasGrounded = isGrounded;
        isGrounded = CheckGrounded();
        
        if (inputLocked) return;
        if (statusController != null && !statusController.CanMove) return;
        
        if (!wasGrounded && isGrounded)
        {
            if (isMidAirJump) PlayerManager.Instance?.NotifyJumpReleased();
            if (isMidAir)     PlayerManager.Instance?.NotifyInAir(false);

            isMidAirJump = false;
            isMidAir     = false;
        }
        else if (wasGrounded && !isGrounded)
        {
            isMidAir = true;
            PlayerManager.Instance?.NotifyInAir(true);
        }
        
        if (rb != null)
        {
            float vy = rb.linearVelocity.y;
            float scale;
            if (isGrounded)
            {
                scale = onGroundGravity;
            }
            else if (vy > 0f) 
            {

                float denom = Mathf.Max(0.01f, lastJumpInitVelY);
                float t = 1f - Mathf.Clamp01(vy / denom); 
                scale = Mathf.Lerp(onGroundGravity, elevationSpeed, t);
            }
            else // chute 
            {
                
                float t = Mathf.Clamp01((-vy) / Mathf.Max(0.01f, fallTerminalReference));
                scale = Mathf.Lerp(elevationSpeed, fallSpeed, t);
            }

            Vector3 gravity = globalGravity * scale * Vector3.up;
            rb.AddForce(gravity, ForceMode.Acceleration);
        }
    }

    private void Update()
    {
        if (isReverting) return;
        
        if (isMenuOn) return;

        if (isPunching)
        {
            punchTimer -= Time.deltaTime;
            punchLogTimer += Time.deltaTime;

            if (punchLogTimer >= punchLogInterval) { punchLogTimer = 0f; }
            if (punchTimer > 0f) return;

            isPunching = false;
        }
        
        Vector2 raw = moveAction.ReadValue<Vector2>();
        float mag2 = raw.sqrMagnitude;
        float on2  = inputOnThreshold  * inputOnThreshold;
        float off2 = inputOffThreshold * inputOffThreshold;
        
        if (!_moveLatched && mag2 >= on2)
        {
            _moveLatched = true;
            _noInputTimer = 0f;
            _lastNonZeroInput = raw;
        }
        else if (_moveLatched)
        {
            if (mag2 >= off2)
            {
                _noInputTimer = 0f;
                _lastNonZeroInput = raw; 
            }
            else
            {
                _noInputTimer += Time.deltaTime;
                float grace = isGrounded ? 0f : airborneStickGrace;
                if (_noInputTimer >= grace)
                    _moveLatched = false;
            }
        }

        bool isMovingNow = _moveLatched;
        if (isMovingNow != _sentMoving)
        {
            PlayerManager.Instance?.NotifyMoving(isMovingNow);
            _sentMoving = isMovingNow;
        }

        if (!isMovingNow) return;
        Vector2 dirForAngle = (mag2 >= off2) ? raw : _lastNonZeroInput;

        var direction = GetDirection(dirForAngle, out bool ok);
        if (!ok) return;

        float dt = Time.deltaTime;
        float stateMult = (statusController != null) ? statusController.SpeedMultiplier : 1f;
        float speed = (isGrounded ? moveSpeed : midAirMoveSpeed) * _runMultCurrent * externalSpeedMult.Value * stateMult;

        float distance = speed * dt;

        IMovement command = CreateCommandInstance(direction, transform, distance);
        command.Move();
        Historique(new MovementRecord(command, dt));
        FreeSpaceFromHistorique();

    }

    private void OnRevertPressed(InputAction.CallbackContext ctx)
    {
        if (revertRoutine != null) return;
        PlayerManager.Instance?.NotifyMoving(false);
        PlayerManager.Instance?.NotifyRun(false);
        revertHeld   = true;
        revertRoutine = StartCoroutine(RevertWhileHeld());
    }

    private void OnRevertReleased(InputAction.CallbackContext ctx)
    {
        revertHeld = false;
    }

    private IEnumerator RevertWhileHeld()
    {
        isReverting = true;
        while (revertHeld && historique.Count > 0)
        {
            int last = historique.Count - 1;
            var rec = historique[last];

            rec.commandRecorder.Revert();
            movementWhileTime -= rec.deltatimeRecorded;
            if (movementWhileTime < 0f) movementWhileTime = 0f;

            historique.RemoveAt(last);
            yield return null;
        }
        isReverting = false;
        revertRoutine = null;
    }

    private void OnRunPressed(InputAction.CallbackContext ctx)
    {
        if (isMenuOn) return;
        PlayerManager.Instance?.NotifyRun(true);
        StartRunLerp(runMult, runRampUpTime); 
    }

    private void OnRunReleased(InputAction.CallbackContext ctx)
    {
        if (isMenuOn) return;
        PlayerManager.Instance?.NotifyRun(false);
        StartRunLerp(1f, runRampDownTime);  
    }

    private void StartRunLerp(float target, float duration)
    {
        if (_runLerpRoutine != null) StopCoroutine(_runLerpRoutine);
        _runLerpRoutine = StartCoroutine(LerpRunMultiplier(target, duration));
    }

    private IEnumerator LerpRunMultiplier(float target, float duration)
    {
        float start = _runMultCurrent;
        if (Mathf.Approximately(duration, 0f))
        {
            _runMultCurrent = target;
            _runLerpRoutine = null;
            yield break;
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, duration);
            _runMultCurrent = Mathf.Lerp(start, target, t);
            yield return null; 
        }

        _runMultCurrent = target;
        _runLerpRoutine = null;
    }


    private void OnJumpPressed(InputAction.CallbackContext ctx)
    {
        if (isMenuOn) return;

        if (isMidAirJump || isMidAir || !isGrounded) return;
   
        
        if (rb != null)
        {
            var v = rb.linearVelocity; v.y = 0f; rb.linearVelocity = v;                
            rb.AddForce(Vector3.up * Force, ForceMode.Impulse);             
            lastJumpInitVelY = Force / Mathf.Max(0.0001f, rb.mass);       
        }

        isMidAirJump = true;                        
        PlayerManager.Instance?.NotifyJumpPressed();  
    }
    private void OnJumpReleased(InputAction.CallbackContext ctx)
    {
        if (isMenuOn) return;
        if (rb != null && rb.linearVelocity.y > 0f)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, rb.linearVelocity.y * 0.6f, rb.linearVelocity.z);
        }
        

    }

    private void OnPunchPressed(InputAction.CallbackContext ctx)
    {
        if (isMenuOn) return;
        PerformedPunch();
    }


    public void OnShootPressed(InputAction.CallbackContext ctx)
    {
        Debug.Log("shoot");
        if(!IsOwner) return;
        if (!ctx.performed) return;
        
        if (isMenuOn) return;
        if (PlayerManager.Instance._isShooting) return;
        
        if (PlayerManager.Instance.currentMagazinP1 <= 0 && currentId == 1) return;
        if (PlayerManager.Instance.currentMagazinP2 <= 0 && currentId == 2) return;


        if (TryGetAimUnitCircleXZ(out var aimXZ))
        {
            if (!projectilePrefab) return;
            
            Quaternion rot = Quaternion.LookRotation(new Vector3(aimXZ.x, 0f, aimXZ.y), Vector3.up);
            

            if (currentId == 1)
            {
                ShootServerRpc(aimXZ, defaultProjectileSpeed, PlayerManager.Instance.ErgonomyP1Final, defaultProjectileLifeTime, currentId, radialCursor.cursorWorldPosition.position, rot);
               
            }

            if (currentId == 2)
            {
                ShootServerRpc(aimXZ, defaultProjectileSpeed, PlayerManager.Instance.ErgonomyP2Final, defaultProjectileLifeTime, currentId, radialCursor.cursorWorldPosition.position, rot);
            }
            
            PlayerManager.Instance.SpendAmmo(1, currentId);
            PlayerManager.Instance.NotifyShootPressed(shootCoolDown);
        }
        
        Debug.Log("shoot end");

    }
    


    [ServerRpc]
    private void ShootServerRpc(Vector2 dirXZ, float speed, float mult, float lifeTime, int ownerPlayerId, Vector3 pos, Quaternion rot)
    {
        GameObject go = Instantiate(projectilePrefab, pos, rot);
        ProjectilPlayer proj = go.GetComponent<ProjectilPlayer>();

        proj.ServerSetupBeforeSpawn(dirXZ, speed, mult, lifeTime, ownerPlayerId);
        
        go.GetComponent<NetworkObject>().Spawn(true);
    }


    
    private bool TryGetAimUnitCircleXZ(out Vector2 unitXZ)
    {
        unitXZ = Vector2.zero;
        if (!radialCursor) return false;
        
        Vector3 v = radialCursor.CurrentWorldPos - transform.position;
        v.y = 0f;

        if (v.sqrMagnitude < 1e-6f) return false; 
        v.Normalize(); 
        unitXZ = new Vector2(v.x, v.z);
        return true;
    }



    private void OnReloadPressed(InputAction.CallbackContext ctx)
    {
        if (isMenuOn) return;

        if (currentId == 1)
        {
            var pm = PlayerManager.Instance;
            if (pm != null)
            {
                bool full = pm.ReloadP1DiscardAndFill();
                if (!full) 
                    Debug.Log("[Reload] P1: rechargement partiel (munitions insuffisantes ou cap=0)");
            }
        }
        
        if (currentId == 2)
        {
            var pm = PlayerManager.Instance;
            if (pm != null)
            {
                bool full = pm.ReloadP2DiscardAndFill();
                if (!full)
                    Debug.Log("[Reload] P1: rechargement partiel (munitions insuffisantes ou cap=0)");
            }
        }

    }
    
    private void OnUnloadPressed(InputAction.CallbackContext ctx)
    {
        if (isMenuOn) return;

        if (currentId == 1)
        {
            PlayerManager.Instance?.UnloadP1();
        }
        if (currentId == 2)
        {
            PlayerManager.Instance?.UnloadP2();
        }

    }

    private void OnPausePressed(InputAction.CallbackContext ctx)
    {
        isMenuOn = !isMenuOn;
    }

    private void PerformedPunch()
    {
        if (isMenuOn) return;
        if (isPunching) return;

        isPunching     = true;
        punchTimer     = Mathf.Max(0f, CoolDown);
        punchLogTimer  = 0f;
        
        PlayerManager.Instance?.NotifyPunchPressed();

        Vector2 input = moveAction.ReadValue<Vector2>();
        bool isMovingNow = input.sqrMagnitude >= inputDeadzone * inputDeadzone;

        if (!isMovingNow)
        {
            PunchStatic();
        }
        else
        {
            var dirEnum = GetDirection(input, out bool ok);
            if (!ok) { PunchStatic(); return; }

            Vector3 worldDir = DirectionToVector(dirEnum);
            PunchMove(worldDir);
        }
    }


 
    public void PunchStatic()
    {
        
    }
    
    public void PunchMove(Vector3 worldDir)
    {
        if (punchRoutine != null) StopCoroutine(punchRoutine);
        punchRoutine = StartCoroutine(PunchMoveRoutine(worldDir.normalized, CoolDown, PunchDistance));
    }

    private float PunchProfileS(float u)
    {
        float k = Mathf.Clamp01(punchEaseStart);
        if (u <= k) return u;
        float t = (u - k) / Mathf.Max(0.0001f, 1f - k);  
        float easeOutCubic = 1f - Mathf.Pow(1f - t, 3f);  
        return k + (1f - k) * easeOutCubic;
    }
    
    private IEnumerator PunchMoveRoutine(Vector3 worldDir, float duration, float distance)
    {
        if (duration <= 0f || distance <= 0f) yield break;

        _punchLastS = 0f;
        float elapsed = 0f;

        while (elapsed < duration && isPunching)
        {
            float u = Mathf.Clamp01(elapsed / duration);
            float s = PunchProfileS(u);
            float ds = s - _punchLastS;
            _punchLastS = s;
            
            Vector3 delta = worldDir * (distance * ds);
            delta.y = 0f;

            if (rb != null) rb.MovePosition(rb.position + delta);
            else              transform.position += delta;

            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;
        }

        punchRoutine = null;
    }
    
    
    public void ApplyDamageServer(int damage, float knockBackForce, Vector2 knockDirXZ, int attackerId)
    {
        if (!IsServer) return;

        currentHealth.Value = Mathf.Max(0, currentHealth.Value - damage);

        var target = new ClientRpcParams {
            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { OwnerClientId } }
        };
        ApplyKnockbackClientRpc(knockDirXZ, knockBackForce, 0.15f, target);
        
        if (statusController != null)
            statusController.TryApplyRandomStateFromAttacker(attackerId);

        if (currentHealth.Value <= 0)
            OnDeathClientRpc();
    }

    

    public void OnDeathClientRpc_Expose()
    {
        if (!IsServer) return;
        OnDeathClientRpc();
    }

    [ClientRpc]
    private void ColorClientRpc()
    {
        if (IsOwner) mat.color = RandomColor();
    }
    [ClientRpc]
    private void OnDeathClientRpc()
    {
        Destroy(gameObject);
    }
    
    [ClientRpc]
    private void ApplyKnockbackClientRpc(Vector2 dirXZ, float force, float stunSeconds, ClientRpcParams rpcParams = default)
    {
        if (!IsOwner || rb == null) return;
        
        var v = rb.linearVelocity; 
        v.y = 0f; 
        rb.linearVelocity = v;

        Vector3 impulse = new Vector3(dirXZ.x, 0f, dirXZ.y);
        if (impulse.sqrMagnitude > 1e-6f) rb.AddForce(impulse.normalized * force, ForceMode.Impulse);
        StartCoroutine(KnockbackLock(stunSeconds));
    }
    
    private IEnumerator KnockbackLock(float seconds)
    {
        knockbackUntil = Time.time + seconds;
        inputLocked = true;
        yield return new WaitForSeconds(seconds);
        inputLocked = false;
    }

    public void SetExternalSpeedMultiplier(float mult)
    {
        if (IsServer)
        {
            externalSpeedMult.Value = Mathf.Clamp(mult, 0.1f, 10f);
        }
        else
        {
            SetExternalSpeedMultiplierServerRpc(mult);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetExternalSpeedMultiplierServerRpc(float mult)
    {
        externalSpeedMult.Value = Mathf.Clamp(mult, 0.1f, 10f);
    }

    
    private void Historique(MovementRecord record)
    {
        historique.Add(record); movementWhileTime += record.deltatimeRecorded;
    }
    private void FreeSpaceFromHistorique()
    {
        while (movementWhileTime > recordWindowSeconds && historique.Count > 0)
        {
            float oldestDeltaTime = historique[0].deltatimeRecorded;
            movementWhileTime -= oldestDeltaTime;
            historique.RemoveAt(0);
        }
    }
    
    private enum Direction 
    {
        Up,
        Down,
        Left,
        Right,
        UpRight,
        UpLeft,
        DownRight,
        DownLeft 
    }

    private Direction GetDirection(Vector2 input, out bool ok)
    {
        ok = true;
        float angle = Mathf.Atan2(input.y, input.x) * Mathf.Rad2Deg; if (angle < 0) angle += 360f;
        int sector = Mathf.RoundToInt(angle / 45f) % 8;
        switch (sector)
        {
            case 0: return Direction.Right; case 1: return Direction.UpRight; case 2: return Direction.Up; case 3: return Direction.UpLeft;
            case 4: return Direction.Left;  case 5: return Direction.DownLeft; case 6: return Direction.Down; case 7: return Direction.DownRight;
            default: ok = false; return Direction.Up;
        }
    }

    private IMovement CreateCommandInstance(Direction direction, Transform t, float distance)
    {
        switch (direction)
        {
            case Direction.Up:        return new MoveUp(t, distance);
            case Direction.Down:      return new MoveDown(t, distance);
            case Direction.Left:      return new MoveLeft(t, distance);
            case Direction.Right:     return new MoveRight(t, distance);
            case Direction.UpRight:   return new MoveUpRight(t, distance);
            case Direction.UpLeft:    return new MoveUpLeft(t, distance);
            case Direction.DownRight: return new MoveDownRight(t, distance);
            case Direction.DownLeft:  return new MoveDownLeft(t, distance);
            default:                  return new MoveUp(t, distance);
        }
    }
    
    private Vector3 DirectionToVector(Direction d)
    {
        switch (d)
        {
            case Direction.Right:     return Vector3.right;
            case Direction.UpRight:   return (Vector3.right + Vector3.forward).normalized;
            case Direction.Up:        return Vector3.forward;
            case Direction.UpLeft:    return (Vector3.forward + Vector3.left).normalized;
            case Direction.Left:      return Vector3.left;
            case Direction.DownLeft:  return (Vector3.left + Vector3.back).normalized;
            case Direction.Down:      return Vector3.back;
            case Direction.DownRight: return (Vector3.right + Vector3.back).normalized;
            default:                  return Vector3.forward;
        }
    }


    private bool CheckGrounded()
    {
        Vector3 centerWorld = transform.TransformPoint(onGroundZoneCenterLocal);
        float half = Mathf.Max(onGroundZoneHeight * 0.5f - onGroundZoneRadius, 0f);
        Vector3 top = centerWorld + Vector3.up * half;
        Vector3 bottom = centerWorld - Vector3.up * half;

        return Physics.CheckCapsule(top, bottom, onGroundZoneRadius, groundLayer, QueryTriggerInteraction.Ignore);
    }

    private void OnDrawGizmosSelected()
    {

        Gizmos.color = isGrounded ? new Color(0f, 1f, 0f, 0.25f) : new Color(1f, 0f, 0f, 0.25f);
        Vector3 centerWorld = transform.TransformPoint(onGroundZoneCenterLocal);
        float half = Mathf.Max(onGroundZoneHeight * 0.5f - onGroundZoneRadius, 0f);
        Vector3 top = centerWorld + Vector3.up * half;
        Vector3 bottom = centerWorld - Vector3.up * half;
        Gizmos.DrawWireSphere(top, onGroundZoneRadius);
        Gizmos.DrawWireSphere(bottom, onGroundZoneRadius);
        Gizmos.DrawLine(top + Vector3.forward*onGroundZoneRadius, bottom + Vector3.forward*onGroundZoneRadius);
        Gizmos.DrawLine(top - Vector3.forward*onGroundZoneRadius, bottom - Vector3.forward*onGroundZoneRadius);
        Gizmos.DrawLine(top + Vector3.right*onGroundZoneRadius,   bottom + Vector3.right*onGroundZoneRadius);
        Gizmos.DrawLine(top - Vector3.right*onGroundZoneRadius,   bottom - Vector3.right*onGroundZoneRadius);
    }

    private Color RandomColor()
    {
        float r = UnityEngine.Random.Range(0f, 1f);
        float g = UnityEngine.Random.Range(0f, 1f);
        float b = UnityEngine.Random.Range(0f, 1f);
        return new Color(r, g, b, 1f);
    }
}
