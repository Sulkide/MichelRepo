using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(10)]
public class RadialCursor8Way : MonoBehaviour
{
    public enum Facing8
    {
        Nord,
        NordEst,
        Est,
        SudEst,
        Sud,
        SudOuest,
        Ouest,
        NordOuest
    }

    public enum MoveDir8
    {
        Right, 
        UpRight,
        Up,
        UpLeft, 
        Left, 
        DownLeft, 
        Down,
        DownRight
    }

    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private string lookActionName  = "Look";   
    [SerializeField] private string pointActionName = "Point";
    public Transform cursorWorldPosition;

    [Header("Ring setting")]
    [SerializeField] private float radiusX = 2f;
    [SerializeField] private float radiusZ = 2f;
    [SerializeField] private float yOffset = 0.02f;
    [SerializeField] private bool billboardToCamera = true;

    [Header("Cursor sprite")]
    [SerializeField] private Sprite cursorSprite;
    [SerializeField] private Vector2 spriteSize = new Vector2(0.3f, 0.3f);

    [Header("Movement direction source")]
    [SerializeField] private bool useActualDisplacement = true;
    [SerializeField] private string moveActionName = "Move";
    [SerializeField] private float moveDeadzone = 0.20f;
    [SerializeField] private float movementEpsilon = 0.0005f;

    [Header("Aiming arbitration")]
    [SerializeField] private float pointerMoveThresholdPixels = 0.5f;
    [SerializeField] private float stickDeadzone = 0.15f;
    [SerializeField] private PlayerManager playerManager;
    private bool latchMoveDirDuringPunch = true;
    private bool _wasPunching;
    private bool _punchLatchedHasValue;
    private MoveDir8 _punchLatchedMove;
    
    public Facing8 CurrentFacing { get; private set; } = Facing8.Nord;
    public MoveDir8 CurrentMoveDir { get; private set; } = MoveDir8.Up;
    public bool IsMoving { get; private set; }
    public Vector3 CurrentWorldPos { get; private set; }
    
    private InputAction _lookAction, _pointAction, _moveAction;
    private Transform _cursorTf;
    private SpriteRenderer _sr;
    private Vector3 _lastPlayerPos;
    private bool _initializedLastPos;

    private Vector2 _prevPointerPos;
    private float _lastPointerTime;
    private float _lastStickTime;
    private Vector3 _lastAimWorldDirXZ; 
    private bool _aimHasValue;      

    private void Awake()
    {
        if (!player) player = transform;
        if (!mainCamera) mainCamera = Camera.main;
        if (!playerManager) playerManager = PlayerManager.Instance; 
        
        if (playerInput)
        {
            var actions = playerInput.actions;
            _lookAction  = actions.FindAction(lookActionName,  false);
            _pointAction = actions.FindAction(pointActionName, false);
            _moveAction  = actions.FindAction(moveActionName,  false);
        }

        var go = new GameObject("RadialCursorSprite");
        go.layer = gameObject.layer;
        _cursorTf = go.transform;
        _cursorTf.SetParent(gameObject.transform, true);
        _sr = go.AddComponent<SpriteRenderer>();
        _sr.sprite = cursorSprite;
        _sr.drawMode = SpriteDrawMode.Sliced;
        _sr.sortingOrder = 1000;
        _sr.size = spriteSize;
        if (!cursorSprite) _sr.color = new Color(1f, 0.3f, 0.3f, 0.85f);
        cursorWorldPosition = _cursorTf;
    }

    private void OnEnable()
    {
        _lookAction?.Enable();
        _pointAction?.Enable();
        _moveAction?.Enable();

        if (_pointAction != null)
            _prevPointerPos = _pointAction.ReadValue<Vector2>();
    }

    private void OnDisable()
    {
        _lookAction?.Disable();
        _pointAction?.Disable();
        _moveAction?.Disable();
    }

    private void Update()
    {
        if (!player) return;

        Vector3 dirXZ = GetInputDirectionOnXZ_Latched();
        if (dirXZ.sqrMagnitude > 1e-6f)
        {
            Vector3 ringPos = ProjectOnEllipseRing(player.position, dirXZ.normalized, radiusX, radiusZ);
            CurrentWorldPos = ringPos;
            CurrentFacing = ComputeFacingFromDir(dirXZ);
            UpdateCursorTransform(ringPos);

            if (!_initializedLastPos)
            {
                _lastPlayerPos = player.position;
                _initializedLastPos = true;
            }
        }
        else
        {
            UpdateCursorTransform(CurrentWorldPos);
            if (!_initializedLastPos)
            {
                _lastPlayerPos = player.position;
                _initializedLastPos = true;
            }
        }
        
        if (useActualDisplacement)
        {
            Vector3 now = player.position;
            Vector3 delta = now - _lastPlayerPos;
            _lastPlayerPos = now;
            delta.y = 0f;

            IsMoving = delta.magnitude > movementEpsilon;
            if (IsMoving)
            {
                float angle = Mathf.Atan2(delta.z, delta.x) * Mathf.Rad2Deg; 
                if (angle < 0) angle += 360f;
                CurrentMoveDir = (MoveDir8)(Mathf.RoundToInt(angle / 45f) % 8);
            }
        }
        else
        {
            Vector2 move = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
            IsMoving = move.sqrMagnitude >= moveDeadzone * moveDeadzone;
            if (IsMoving)
            {
                float angle = Mathf.Atan2(move.y, move.x) * Mathf.Rad2Deg;
                if (angle < 0) angle += 360f;
                CurrentMoveDir = (MoveDir8)(Mathf.RoundToInt(angle / 45f) % 8);
            }
        }
        bool punching = playerManager && playerManager.IsPunching;
        if (latchMoveDirDuringPunch && punching)
        {
            if (!_wasPunching || !_punchLatchedHasValue)
            {
                _punchLatchedHasValue = true;

                if (IsMoving)
                    _punchLatchedMove = CurrentMoveDir;
                else
                {
                    Vector2 mv = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
                    if (mv.sqrMagnitude >= moveDeadzone * moveDeadzone)
                    {
                        _punchLatchedMove = MoveFromVec2(mv);
                    }
                    else if (dirXZ.sqrMagnitude > 1e-6f)
                    {
                        _punchLatchedMove = MoveFromXZ(dirXZ);
                    }
                    else
                    {
                        _punchLatchedMove = CurrentMoveDir;
                    }
                }
            }
            
            IsMoving = true;
            CurrentMoveDir = _punchLatchedMove;
        }
        else if (_wasPunching && !punching)
        {
            _punchLatchedHasValue = false;
        }

        _wasPunching = punching;

    }
    
    private static MoveDir8 MoveFromXZ(Vector3 v)
    {
        if (v.sqrMagnitude < 1e-8f) return MoveDir8.Up;
        float angle = Mathf.Atan2(v.z, v.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360f;
        return (MoveDir8)(Mathf.RoundToInt(angle / 45f) % 8);
    }

    private static MoveDir8 MoveFromVec2(Vector2 v)
    {
        if (v.sqrMagnitude < 1e-8f) return MoveDir8.Up;
        float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg; 
        if (angle < 0) angle += 360f;
        return (MoveDir8)(Mathf.RoundToInt(angle / 45f) % 8);
    }


    private void UpdateCursorTransform(Vector3 worldPos)
    {
        _cursorTf.position = new Vector3(worldPos.x, player.position.y + yOffset, worldPos.z);
        if (billboardToCamera && mainCamera)
            _cursorTf.forward = (mainCamera.transform.position - _cursorTf.position).normalized;
        else
            _cursorTf.up = Vector3.up;
    }
    
    private Vector3 GetInputDirectionOnXZ_Latched()
    {
        bool pointerMoved = false;
        bool stickActive  = false;
        
        Vector3 vPointer = Vector3.zero;
        if (_pointAction != null && _pointAction.enabled && mainCamera)
        {
            Vector2 pointerPos = _pointAction.ReadValue<Vector2>();
            float sq = (pointerPos - _prevPointerPos).sqrMagnitude;
            pointerMoved = sq >= (pointerMoveThresholdPixels * pointerMoveThresholdPixels);

            if (pointerMoved)
            {
                _lastPointerTime = Time.unscaledTime;
                _prevPointerPos  = pointerPos;

                var ray = mainCamera.ScreenPointToRay(pointerPos);
                Plane plane = new Plane(Vector3.up, new Vector3(0f, player.position.y + yOffset, 0f));
                if (plane.Raycast(ray, out float enter))
                {
                    Vector3 hit = ray.GetPoint(enter);
                    vPointer = hit - player.position;
                    vPointer.y = 0f;
                    if (vPointer.sqrMagnitude > 0f)
                    {
                        _lastAimWorldDirXZ = vPointer.normalized;
                        _aimHasValue = true;
                    }
                }
            }
        }
        
        Vector3 vStick = Vector3.zero;
        if (_lookAction != null && _lookAction.enabled)
        {
            Vector2 look = _lookAction.ReadValue<Vector2>();
            stickActive = look.sqrMagnitude >= stickDeadzone * stickDeadzone;
            if (stickActive)
            {
                _lastStickTime = Time.unscaledTime;
                vStick = new Vector3(look.x, 0f, look.y);
                if (vStick.sqrMagnitude > 0f)
                {
                    _lastAimWorldDirXZ = vStick.normalized;
                    _aimHasValue = true;
                }
            }
        }

        if (pointerMoved && stickActive)
        {
            if (_lastPointerTime >= _lastStickTime) return vPointer;
            return vStick;
        }
        if (pointerMoved) return vPointer;
        if (stickActive)  return vStick;
        
        return _aimHasValue ? _lastAimWorldDirXZ : Vector3.zero;
    }

    private static Vector3 ProjectOnEllipseRing(Vector3 center, Vector3 dirXZ, float rx, float rz)
    {
        Vector2 e = new Vector2(dirXZ.x / Mathf.Max(rx, 0.0001f), dirXZ.z / Mathf.Max(rz, 0.0001f));
        if (e.sqrMagnitude > 0f) e.Normalize();
        Vector3 onRing = new Vector3(e.x * rx, 0f, e.y * rz);
        return center + onRing;
    }

    private static Facing8 ComputeFacingFromDir(Vector3 dirXZ)
    {
        if (dirXZ.sqrMagnitude < 1e-6f) return Facing8.Nord;
        float angleFromNorth = Mathf.Atan2(dirXZ.x, dirXZ.z) * Mathf.Rad2Deg;
        if (angleFromNorth < 0f) angleFromNorth += 360f;
        int sector = Mathf.RoundToInt(angleFromNorth / 45f) % 8;
        return (Facing8)sector;
    }

    private void OnDestroy()
    {
        if (_cursorTf) Destroy(_cursorTf.gameObject);
    }
}
