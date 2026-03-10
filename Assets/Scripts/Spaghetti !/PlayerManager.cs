using UnityEngine;
using System;
using UnityEngine.Serialization;

public class PlayerManager : MonoBehaviour
{

    public static PlayerManager Instance { get; private set; }

    [Header("helper")]
    /*[SerializeField]*/public float punchCooldown = 0.5f;
    public bool _isPunching = false;
    /*[SerializeField]*/public float _punchTimer = 0f;
    public bool IsPunching => _isPunching;                
    public float _punchLogTimer = 0f;
    public float _punchLogInterval = 0.2f; 
    
    public bool isMenuOn;
    public bool _isMoving;
    public bool _isRunHeld;
    public bool _isJumpHeld;
    public bool _isInAir; 
    
    [Header("shoot")]
    /*[SerializeField]*/ public bool shootExtendOnRetrigger = true; 
    /*[HideInInspector]*/ public bool _isShooting = false;
    public float _shootTimer = 0f;
    public bool IsShooting => _isShooting;
    
    private int nextPlayerId = 1;
    [Header("Multiplier")]
    [SerializeField] private float pickupMultP1 = 1f;
    [SerializeField] private float pickupMultP2 = 1f;

    
    [FormerlySerializedAs("player")] [Header("reférence players")]
    public GameObject player1;
    public GameObject player2;
    
    [Header("equipment Ref")]
    [SerializeField] private EquipmentManager equipP1;
    [SerializeField] private EquipmentManager equipP2;

    [Header("Magazines")]
    public int currentMagazinP1;
    public int currentMagazinP2;
    
    [Header("equipped Weapon stats")]
    public int DamageP1, DamageP2;
    public int MaxMagazinP1, MaxMagazinP2;
    public float MaxPressureLevelP1, MaxPressureLevelP2;
    public int ReloadTimeP1, ReloadTimeP2;
    public float ErgonomyP1, ErgonomyP2;
    
 
    [Header("Accessory modifiers")]
    public int AddDamageP1, AddDamageP2;
    public int AddMaxMagazinP1, AddMaxMagazinP2;
    public float MultPressureLevelP1 = 1f, MultPressureLevelP2 = 1f;
    public int AddReloadTimeP1, AddReloadTimeP2;
    public float MultErgonomyP1 = 1f, MultErgonomyP2 = 1f;

 
    [Header("final dtats")]
    public int DamageP1Final, DamageP2Final;
    public int MaxMagazinP1Final, MaxMagazinP2Final;
    public float MaxPressureLevelP1Final, MaxPressureLevelP2Final;
    public int ReloadTimeP1Final, ReloadTimeP2Final;
    public float ErgonomyP1Final, ErgonomyP2Final;


 
    [Header("equipped Ammo")]
    public int maxStackP1, maxStackP2;

    [Range(0,100)] public float poisonP1;
    [Range(0,100)] public float spleepyP1;
    [Range(0,100)] public float hemorrhageP1;
    [Range(0,100)] public float weaknessP1;
    [Range(0,100)] public float slownessP1;

    [Range(0,100)] public float poisonP2;
    [Range(0,100)] public float spleepyP2;
    [Range(0,100)] public float hemorrhageP2;
    [Range(0,100)] public float weaknessP2;
    [Range(0,100)] public float slownessP2;

 
    private AmmoDefinition _lastAmmoDefP1, _lastAmmoDefP2;

 
    private WeaponDefinition _lastWeaponDefP1, _lastWeaponDefP2;

 
    [SerializeField] private float equipPollInterval = 0.20f;
    private float _equipPollTimer;


    public enum PlayerState
    {
        Idle,
        Walk,
        Run, 
        Jump,
        InAir,
        Punch,
        Shoot
    }

    [SerializeField] private PlayerState _state = PlayerState.Idle;
    public PlayerState State => _state;

    public static event Action<bool> OnMenuStateChanged;
    public event Action<PlayerState> OnStateChanged;
 
    public enum DebugLogMode { Off, OnChange, Interval, EveryFrame }

    [Header("Debug (Radial Cursor)")]
    [SerializeField] private RadialCursor8Way radial;
    [SerializeField] private DebugLogMode debugLogMode = DebugLogMode.Interval;
    [SerializeField] private float logIntervalSeconds = 0.25f;

    [SerializeField] private bool showHUD = true;
    [SerializeField] private Vector2 hudPos = new Vector2(12, 12);
    [SerializeField] private int hudFontSize = 14;

    [SerializeField] private bool debugDrawVectors = true;
    [SerializeField] private float debugVectorLength = 1.6f;

    private float _logTimer;
    private GUIStyle _hudStyle;

    private RadialCursor8Way.Facing8 _prevFacing;
    private RadialCursor8Way.MoveDir8 _prevMove;
    private bool _prevRadialIsMoving;

    private static readonly string[] FacingFR = { "nord","nord est","est","sud est","sud","sud ouest","ouest","nord ouest" };
    private static readonly string[] MoveDirLabels = { "right","up right","up","up left","left","down left","down","down right" };
    private static readonly string[] StateLabels = { "Idle", "Walk", "Run", "Jump", "InAir", "Punch", "Shoot" };


    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        RefreshEquippedWeaponStats();
        _equipPollTimer = equipPollInterval;
    }

    private void Update()
    {
        _equipPollTimer -= Time.unscaledDeltaTime;
        if (_equipPollTimer <= 0f)
        {
            _equipPollTimer = equipPollInterval;
            RefreshEquippedWeaponStats();
        }
 
        if (_isShooting)
        {
            _shootTimer -= Time.deltaTime;
            if (_shootTimer <= 0f)
            {
                _isShooting = false;
  
                Evaluate(); 
            }
        }

        
 
        if (_isPunching)
        {
            _punchTimer -= Time.deltaTime;
            _punchLogTimer += Time.deltaTime;
            if (_punchLogTimer >= _punchLogInterval)
            {
                _punchLogTimer = 0f;
 
            }

            if (_punchTimer <= 0f)
            {
                _isPunching = false;
 
                Evaluate();  
            }
        }

 
        if (radial)
        {
            bool radialMoving = radial.IsMoving;
            var facing = radial.CurrentFacing;
            var move = radial.CurrentMoveDir;

            bool changed = (facing != _prevFacing) || (move != _prevMove) || (radialMoving != _prevRadialIsMoving);

            if (debugLogMode != DebugLogMode.Off)
            {
                string msg = radialMoving
                    ? $"player is facing : {FacingFR[(int)facing]},  with direction : {MoveDirLabels[(int)move]}"
                    : $"player is facing : {FacingFR[(int)facing]},  with direction : ne bouge pas";

                switch (debugLogMode)
                {
                    case DebugLogMode.OnChange:
                        if (changed) Debug.Log(msg);
                        break;
                    case DebugLogMode.Interval:
                        _logTimer += Time.unscaledDeltaTime;
                        if (_logTimer >= logIntervalSeconds)
                        {
                            _logTimer = 0f;
                            Debug.Log(msg);
                        }
                        break;
                    case DebugLogMode.EveryFrame:
                        Debug.Log(msg);
                        break;
                }
            }

            if (debugDrawVectors)
            {
                Vector3 origin = radial.transform.position + Vector3.up * 0.1f;

                Vector3 facingDir = FacingToWorld(facing);
                Debug.DrawRay(origin, facingDir * debugVectorLength, Color.cyan);

                Vector3 moveDir = radialMoving ? MoveDirToWorld(move) : Vector3.zero;
                Debug.DrawRay(origin, moveDir * debugVectorLength, radialMoving ? Color.green : Color.red);
            }

            _prevFacing = facing;
            _prevMove = move;
            _prevRadialIsMoving = radialMoving;
        }
    }
    
    public int GetNextPlayerId(int id)
    {
        id = nextPlayerId;
        nextPlayerId++;
        return id;
    }

    public void GetRadialCursor8Way(RadialCursor8Way cursor8Way)
    {
        radial = cursor8Way;
    }
    private void ComputeAccessoryModifiersFor(EquipmentManager em,
    out int addDmg, out int addMag, out float multPress, out int addReload, out float multErgo)
    {
        addDmg = 0;
        addMag = 0;
        addReload = 0;
        multPress = 1f;       
        multErgo  = 1f;

        if (em == null) return;

        int count = em.SlotCount;
        for (int i = 0; i < count; i++)
        {
            var inst = em.GetByIndex(i);
            if (inst == null || inst.Definition == null) continue;
            if (inst.Definition.Type != ItemType.Accessory) continue;
            var acc = inst.Definition as AccessoryDefinition;
            if (acc == null) continue;

            addDmg   += acc.addDamage;
            addMag   += acc.addMaxMagazin;
            multPress *= acc.multPressureLevel;   
            addReload += acc.addReloadTime;
            multErgo  *= acc.multErgonomic;       
        }
    }

    private void RecomputeAccessoryModifiersAndFinals()
    {
 
        ComputeAccessoryModifiersFor(equipP1,
            out AddDamageP1, out AddMaxMagazinP1, out MultPressureLevelP1, out AddReloadTimeP1, out MultErgonomyP1);

        ComputeAccessoryModifiersFor(equipP2,
            out AddDamageP2, out AddMaxMagazinP2, out MultPressureLevelP2, out AddReloadTimeP2, out MultErgonomyP2);
        // P1
        DamageP1Final           = Mathf.RoundToInt((DamageP1 + AddDamageP1) * pickupMultP1);
        MaxMagazinP1Final       = Mathf.Max(0, Mathf.RoundToInt((MaxMagazinP1 + AddMaxMagazinP1) * pickupMultP1));
        MaxPressureLevelP1Final = Mathf.Max(0f, (MaxPressureLevelP1 * MultPressureLevelP1) * pickupMultP1);
        ReloadTimeP1Final       = Mathf.Max(0, Mathf.RoundToInt((ReloadTimeP1 + AddReloadTimeP1) * pickupMultP1));
        ErgonomyP1Final         = Mathf.Max(0f, (ErgonomyP1 * MultErgonomyP1) * pickupMultP1);

        // P2
        DamageP2Final           = Mathf.RoundToInt((DamageP2 + AddDamageP2) * pickupMultP2);
        MaxMagazinP2Final       = Mathf.Max(0, Mathf.RoundToInt((MaxMagazinP2 + AddMaxMagazinP2) * pickupMultP2));
        MaxPressureLevelP2Final = Mathf.Max(0f, (MaxPressureLevelP2 * MultPressureLevelP2) * pickupMultP2);
        ReloadTimeP2Final       = Mathf.Max(0, Mathf.RoundToInt((ReloadTimeP2 + AddReloadTimeP2) * pickupMultP2));
        ErgonomyP2Final         = Mathf.Max(0f, (ErgonomyP2 * MultErgonomyP2) * pickupMultP2);

    }

    
    public  void BindEquipmentManagersByPlayerID(EquipmentManager em, int id)
    {
        if (id == 1)
        {
            equipP1 = em;
        }

        if (id == 2)
        {
            equipP2 = em;
        }
        
        
        if (equipP1 == null)
            Debug.LogWarning("[PlayerManager] Aucun EquipmentManager avec PlayerID=1 trouvé.");
        if (equipP2 == null)
            Debug.LogWarning("[PlayerManager] Aucun EquipmentManager avec PlayerID=2 trouvé.");

    }

    public void AssignPlayerRef(GameObject player, int id)
    {
        if (id == 1)
        {
            player1 = player;
        }

        if (id == 2)
        {
            player2 = player;
        }
    }

    private static WeaponDefinition GetEquippedWeaponDef(EquipmentManager em)
    {
        if (em == null) return null;
 
        var inst = em.GetByIndex(0);
        return inst != null ? inst.Definition as WeaponDefinition : null;
    }

    public void RefreshEquippedWeaponStats()
    {
 
        //P1
        var w1 = GetEquippedWeaponDef(equipP1);
        if (!ReferenceEquals(w1, _lastWeaponDefP1))
        {
            _lastWeaponDefP1 = w1;
            if (w1 != null)
            {
                DamageP1 = w1.Damage;
                MaxMagazinP1 = w1.maxMagazin;
                MaxPressureLevelP1 = w1.maxPressureLevel;
                ReloadTimeP1 = w1.reloadTime;
                ErgonomyP1 = w1.ergonomy;
            }
            else
            {
                DamageP1 = 0; MaxMagazinP1 = 0; MaxPressureLevelP1 = 0f; ReloadTimeP1 = 0; ErgonomyP1 = 0f;
            }
        }

        //P2 
        var w2 = GetEquippedWeaponDef(equipP2);
        if (!ReferenceEquals(w2, _lastWeaponDefP2))
        {
            _lastWeaponDefP2 = w2;
            if (w2 != null)
            {
                DamageP2 = w2.Damage;
                MaxMagazinP2 = w2.maxMagazin;
                MaxPressureLevelP2 = w2.maxPressureLevel;
                ReloadTimeP2 = w2.reloadTime;
                ErgonomyP2 = w2.ergonomy;
            }
            else
            {
                DamageP2 = 0; MaxMagazinP2 = 0; MaxPressureLevelP2 = 0f; ReloadTimeP2 = 0; ErgonomyP2 = 0f;
            }
        }

  
        RecomputeAccessoryModifiersAndFinals();
        RefreshEquippedAmmoStats();
    }

    public (float poison, float sleepy, float hemorrhage, float weakness, float slowness) GetStatusChances(int attackerId)
    {
        if (attackerId == 1)
            return (poisonP1, spleepyP1, hemorrhageP1, weaknessP1, slownessP1);

        if (attackerId == 2)
            return (poisonP2, spleepyP2, hemorrhageP2, weaknessP2, slownessP2);

        return (0f, 0f, 0f, 0f, 0f);
    }

    public PlayerMovement GetPlayerMovementById(int id)
    {
        if (id == 1 && player1 != null) return player1.GetComponent<PlayerMovement>();
        if (id == 2 && player2 != null) return player2.GetComponent<PlayerMovement>();
        return null;
    }

    
    public void ForceRefreshEquipment()
    {
        _lastWeaponDefP1 = null;
        _lastWeaponDefP2 = null;
        RefreshEquippedWeaponStats();
    }
    
    private static AmmoDefinition GetEquippedAmmoDef(EquipmentManager em)
    {
        if (em == null) return null;
        int n = em.SlotCount;
        if (n <= 0) return null;
        var inst = em.GetByIndex(n - 1);
        return inst != null ? inst.Definition as AmmoDefinition : null;
    }

    
    
    private void RefreshEquippedAmmoStats()
    {
        //P1
        var a1 = GetEquippedAmmoDef(equipP1);
        if (!ReferenceEquals(a1, _lastAmmoDefP1))
        {
            _lastAmmoDefP1 = a1;

            if (a1 != null)
            {
                maxStackP1  = a1.MaxStack;
                poisonP1    = Mathf.Clamp(a1.poison,     0f, 100f);
                spleepyP1   = Mathf.Clamp(a1.spleepy,    0f, 100f);
                hemorrhageP1= Mathf.Clamp(a1.hemorrhage, 0f, 100f);
                weaknessP1  = Mathf.Clamp(a1.weakness,   0f, 100f);
                slownessP1  = Mathf.Clamp(a1.slowness,   0f, 100f);
            }
            else
            {
                maxStackP1 = 0;
                poisonP1 = spleepyP1 = hemorrhageP1 = weaknessP1 = slownessP1 = 0f;
            }
            // Debug.Log($"[Ammo] P1 -> MaxStack {maxStackP1}, poison {poisonP1}, sleepy {spleepyP1}...");
        }

        //P2
        var a2 = GetEquippedAmmoDef(equipP2);
        if (!ReferenceEquals(a2, _lastAmmoDefP2))
        {
            _lastAmmoDefP2 = a2;

            if (a2 != null)
            {
                maxStackP2  = a2.MaxStack;
                poisonP2    = Mathf.Clamp(a2.poison,     0f, 100f);
                spleepyP2   = Mathf.Clamp(a2.spleepy,    0f, 100f);
                hemorrhageP2= Mathf.Clamp(a2.hemorrhage, 0f, 100f);
                weaknessP2  = Mathf.Clamp(a2.weakness,   0f, 100f);
                slownessP2  = Mathf.Clamp(a2.slowness,   0f, 100f);
            }
            else
            {
                maxStackP2 = 0;
                poisonP2 = spleepyP2 = hemorrhageP2 = weaknessP2 = slownessP2 = 0f;
            }
            // Debug.Log($"[Ammo] P2 -> MaxStack {maxStackP2}, poison {poisonP2}, sleepy {spleepyP2}...");
        }
    }
    

    private ItemInstance GetEquippedAmmoInstance(EquipmentManager em)
    {
        if (em == null) return null;
        int n = em.SlotCount;
        if (n <= 0) return null;
        return em.GetByIndex(n - 1);
    }

    public void SpendAmmo(int ammo, int playerID)
    {
        if (playerID == 1)
        {
            currentMagazinP1 -= ammo;
        }

        if (playerID == 2)
        {
            currentMagazinP2 -= ammo;
        }
    }
    
    public int UnloadP1()
    {
        int moved = 0;
        int toReturn = currentMagazinP1;
        if (toReturn <= 0) return 0;

        var ammo = GetEquippedAmmoInstance(equipP1);
        if (ammo == null || ammo.Definition == null || ammo.Definition.Type != ItemType.Ammo)
        {
            Debug.Log("[Unload] P1: aucune ammo équipée. Les balles sont jetées.");
            currentMagazinP1 = 0;
            return 0;
        }

  
        if (ammo.Definition.Stackable && ammo.Definition.MaxStack > 0)
        {
            int capacity = ammo.Definition.MaxStack - ammo.Quantity;

            if (capacity >= toReturn)
            {
                ammo.Quantity += toReturn;
                moved = toReturn;
            }
            else
            {
                ammo.Quantity = ammo.Definition.MaxStack;
                moved = capacity;
                
                int leftover = toReturn - capacity;
                Inventory inv = (equipP1 != null) ? equipP1.GetComponent<Inventory>() : null;
                if (leftover > 0 && inv != null)
                {
                    inv.Add(new ItemInstance(ammo.Definition, leftover));
                    moved += leftover;
                }
            }
        }
        else
        {
            ammo.Quantity += toReturn;
            moved = toReturn;
        }

        currentMagazinP1 = 0;
        return moved;
    }
    
    public int UnloadP2()
    {
        int moved = 0;
        int toReturn = currentMagazinP2;
        if (toReturn <= 0) return 0;

        var ammo = GetEquippedAmmoInstance(equipP2);
        if (ammo == null || ammo.Definition == null || ammo.Definition.Type != ItemType.Ammo)
        {
            currentMagazinP2 = 0;
            return 0;
        }

        if (ammo.Definition.Stackable && ammo.Definition.MaxStack > 0)
        {
            int capacity = ammo.Definition.MaxStack - ammo.Quantity;

            if (capacity >= toReturn)
            {
                ammo.Quantity += toReturn;
                moved = toReturn;
            }
            else
            {
                ammo.Quantity = ammo.Definition.MaxStack;
                moved = capacity;
                int leftover = toReturn - capacity;
                Inventory inv = (equipP2 != null) ? equipP2.GetComponent<Inventory>() : null;
                if (leftover > 0 && inv != null)
                {
                    inv.Add(new ItemInstance(ammo.Definition, leftover));
                    moved += leftover;
                }

            }
        }
        else
        {
            ammo.Quantity += toReturn;
            moved = toReturn;
        }

        currentMagazinP2 = 0;
        return moved;
    }
    
    public bool ReloadP1DiscardAndFill()
    {
        RefreshEquippedWeaponStats();

        if (equipP1 == null) return false;

        int cap = Mathf.Max(0, MaxMagazinP1Final);
        currentMagazinP1 = 0;

        if (cap <= 0) return false;
  
        var ammo = GetEquippedAmmoInstance(equipP1);
        
        if (ammo == null || ammo.Definition == null || ammo.Definition.Type != ItemType.Ammo)return false;
        
        if (ammo.Quantity <= 0) return false;
        
        int take = Mathf.Min(cap, ammo.Quantity);
        ammo.Quantity -= take;
        currentMagazinP1 = take;
        
        return take == cap;
    }

    public bool ReloadP2DiscardAndFill()
    {
        RefreshEquippedWeaponStats();

        if (equipP2 == null) return false;
        
        int cap = Mathf.Max(0, MaxMagazinP2Final);
        
        currentMagazinP2 = 0;

        if (cap <= 0) return false;

        var ammo = GetEquippedAmmoInstance(equipP2);
        
        if (ammo == null || ammo.Definition == null || ammo.Definition.Type != ItemType.Ammo) return false;

        if (ammo.Quantity <= 0) return false;


        int take = Mathf.Min(cap, ammo.Quantity);
        ammo.Quantity -= take;
        currentMagazinP2 = take;
        
        return take == cap;
    }
    
    public void ApplyPickupMultiplier(int playerId, float mult)
    {
        mult = Mathf.Max(0.0001f, mult);

        if (playerId == 1) pickupMultP1 *= mult;
        else if (playerId == 2) pickupMultP2 *= mult;
        
        RecomputeAccessoryModifiersAndFinals();
    }

    public int GetMaxMagazinFinal(int playerId)
    {
        return playerId == 1 ? MaxMagazinP1Final : MaxMagazinP2Final;
    }

    public float GetErgonomyFinal(int playerId)
    {
        return playerId == 1 ? ErgonomyP1Final : ErgonomyP2Final;
    }




    
    private void OnGUI() //chat gpt
    {
        if (!showHUD) return;

        if (_hudStyle == null)
        {
            _hudStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = hudFontSize,
                normal = { textColor = Color.white }
            };
        }

        string line1 = radial ? $"Facing : {FacingFR[(int)radial.CurrentFacing]}" : "Facing : (n/a)";
        string line2 = radial
            ? (radial.IsMoving
                ? $"Direction : {MoveDirLabels[(int)radial.CurrentMoveDir]}"
                : "Direction : ne bouge pas")
            : "Direction : (n/a)";
        int si = Mathf.Clamp((int)_state, 0, StateLabels.Length - 1);
        string line3 = $"État : {StateLabels[si]}";

        string line4 = $"Mag P1: {currentMagazinP1}/{MaxMagazinP1Final}";
        GUI.Label(new Rect(hudPos.x, hudPos.y, 600, 100), $"{line1}\n{line2}\n{line3}\n{line4}", _hudStyle);

    }

    public void NotifyMoving(bool moving)
    {
        _isMoving = moving;
        Evaluate();
    }

    public void NotifyRun(bool held)
    {
        _isRunHeld = held;
        Evaluate();
    }
    

    public void NotifyInAir(bool inAir)  
    {
        _isInAir = inAir;
        Evaluate();
    }

    
    public void NotifyPunchPressed()
    {
        if (_isPunching) return;
        _isPunching = true;
        _punchTimer = Mathf.Max(0f, punchCooldown);
        _punchLogTimer = 0f;
        SetState(PlayerState.Punch);
    }
    
    public void NotifyShootPressed(float time)
    {
        float t = Mathf.Max(0f, time);
        if (t <= 0f) return;
        _isShooting = true;
        
        _shootTimer = shootExtendOnRetrigger ? Mathf.Max(_shootTimer, t) : t;
        
        SetState(PlayerState.Shoot);
    }

    public void NotifyJumpPressed()
    {
        _isJumpHeld = true;
        SetState(PlayerState.Jump);
    }

    public void NotifyJumpReleased()
    {
        _isJumpHeld = false;
        Evaluate();
    }

    public void MenuOpen()
    {
        isMenuOn = true;
        radial.enabled = false;
        OnMenuStateChanged?.Invoke(isMenuOn);
    }

    public void MenuClose()
    {
        isMenuOn = false;
        radial.enabled = true;
        OnMenuStateChanged?.Invoke(isMenuOn);
    }

    void Evaluate()
    {
        PlayerState next;
        if (_isShooting)                  next = PlayerState.Shoot; 
        else if (_isPunching)             next = PlayerState.Punch;
        else if (_isJumpHeld)             next = PlayerState.Jump;
        else if (_isInAir)                next = PlayerState.InAir;
        else if (_isMoving && _isRunHeld) next = PlayerState.Run;
        else if (_isMoving)               next = PlayerState.Walk;
        else                              next = PlayerState.Idle;

        SetState(next);
    }



    private void SetState(PlayerState next)
    {
        if (_state == next) return;
        _state = next;
        OnStateChanged?.Invoke(_state);
    }
    
    private static Vector3 FacingToWorld(RadialCursor8Way.Facing8 f)
    {
        switch (f)
        {
            case RadialCursor8Way.Facing8.Nord:      return Vector3.forward;
            case RadialCursor8Way.Facing8.NordEst:   return (Vector3.forward + Vector3.right).normalized;
            case RadialCursor8Way.Facing8.Est:       return Vector3.right;
            case RadialCursor8Way.Facing8.SudEst:    return (Vector3.back + Vector3.right).normalized;
            case RadialCursor8Way.Facing8.Sud:       return Vector3.back;
            case RadialCursor8Way.Facing8.SudOuest:  return (Vector3.back + Vector3.left).normalized;
            case RadialCursor8Way.Facing8.Ouest:     return Vector3.left;
            case RadialCursor8Way.Facing8.NordOuest: return (Vector3.forward + Vector3.left).normalized;
            default:                                  return Vector3.forward;
        }
    }

    private static Vector3 MoveDirToWorld(RadialCursor8Way.MoveDir8 d)
    {
        switch (d)
        {
            case RadialCursor8Way.MoveDir8.Right:     return Vector3.right;
            case RadialCursor8Way.MoveDir8.UpRight:   return (Vector3.right + Vector3.forward).normalized;
            case RadialCursor8Way.MoveDir8.Up:        return Vector3.forward;
            case RadialCursor8Way.MoveDir8.UpLeft:    return (Vector3.forward + Vector3.left).normalized;
            case RadialCursor8Way.MoveDir8.Left:      return Vector3.left;
            case RadialCursor8Way.MoveDir8.DownLeft:  return (Vector3.left + Vector3.back).normalized;
            case RadialCursor8Way.MoveDir8.Down:      return Vector3.back;
            case RadialCursor8Way.MoveDir8.DownRight: return (Vector3.right + Vector3.back).normalized;
            default:                                   return Vector3.forward;
        }
    }
}
