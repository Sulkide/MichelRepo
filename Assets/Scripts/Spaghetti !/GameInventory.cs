using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameInventory : MonoBehaviour
{
    private enum MainTab { Equipment, Stats, Options }
    private enum PanelFocus { MainTabs, SubTabs, LeftSlots, RightInventory }
    private enum SubCategory { Weapon, Accessory, Ammo, Consumable }
    private enum SelSource { None, Inventory, Slot }
    private struct SlotRef { public EquipmentManager mgr; public int slot; }


    [Header("References")]
    public Inventory inventory;
    public EquipmentManager equipmentManager1;  
    public EquipmentManager equipmentManager2;  
    public PlayerMovement playerMovement;
    public PlayerInput playerInput;

    [Header("Optional")]
    public PlayerManager playerManager;         

    [Header("Input actions (names in your Input Actions)")]
    [SerializeField] private string actionMapName = "Gameplay";
    [SerializeField] private string moveActionName = "Move";
    [SerializeField] private string jumpActionName = "Jump";
    [SerializeField] private string shootActionName = "Shoot";
    [SerializeField] private string pauseActionName = "Pause";

    private InputAction moveAction, jumpAction, shootAction, pauseAction;
    
    private MainTab _main = MainTab.Equipment;
    private PanelFocus _focus = PanelFocus.RightInventory;
    private SubCategory _cat = SubCategory.Weapon;
    
    private int _rightIndex = 0;
    private readonly List<int> _filteredIdx = new();
    
    private int _leftListIndex = 0;
    private readonly List<SlotRef> _allowedSlots = new();

    private SelSource _selSource = SelSource.None;
    private int _selInvFilteredIndex = -1;
    private int _selSlotListIndex = -1;

    private Vector2 _lastMove;
    private float _navCooldown;
    private const float FirstRepeatDelay = 0.25f;
    private const float NextRepeatRate   = 0.08f;


    private Rect _mainTabsRect, _subTabsRect, _leftRect, _midRect, _detailRect;
    private Vector2 _detailScroll;


    private int _lastInvCount = -1;
    private int _lastSlotsCombined = -1;

    // ===== Setup =====
    private void Awake()
    {
        if (!playerInput)    playerInput = GetComponent<PlayerInput>();
        if (!playerMovement) playerMovement = GetComponent<PlayerMovement>();
        if (!inventory)      inventory = GetComponent<Inventory>();
        if (!playerManager)  playerManager = PlayerManager.Instance;

        // Auto-détection de 2 EquipmentManager si non assignés
        if (equipmentManager1 == null || equipmentManager2 == null)
        {
            var all = FindObjectsOfType<EquipmentManager>(true);
            foreach (var em in all)
            {
                if (em.PlayerID == 1 && equipmentManager1 == null) equipmentManager1 = em;
                if (em.PlayerID == 2 && equipmentManager2 == null) equipmentManager2 = em;
            }
            if (equipmentManager1 == null && all.Length > 0) equipmentManager1 = all[0];
            if (equipmentManager2 == null && all.Length > 1) equipmentManager2 = all[1];
        }
    }

    private void OnEnable()
    {
        if (!playerInput) return;
        var actions = playerInput.actions;
        if (!string.IsNullOrEmpty(actionMapName)) actions.FindActionMap(actionMapName, true);

        moveAction  = actions[moveActionName];
        jumpAction  = actions[jumpActionName];
        shootAction = actions[shootActionName];
        pauseAction = actions[pauseActionName];

        jumpAction.performed  += OnConfirmPressed;
        shootAction.performed += OnConfirmPressed;
        pauseAction.performed += OnPausePressed;

        moveAction?.Enable();
        jumpAction?.Enable();
        shootAction?.Enable();
        pauseAction?.Enable();

        RebuildAll();
    }

    private void OnDisable()
    {
        if (jumpAction  != null) jumpAction.performed  -= OnConfirmPressed;
        if (shootAction != null) shootAction.performed -= OnConfirmPressed;
        if (pauseAction != null) pauseAction.performed -= OnPausePressed;
    }

    // ===== Menu control =====
    public void ToggleMenu()
    {
        if (playerMovement == null) return;
        if (playerMovement.isMenuOn) CloseMenu(); else OpenMenu();
    }

    public void OpenMenu()
    {
        if (playerMovement == null) return;
        _main  = MainTab.Equipment;
        _focus = PanelFocus.RightInventory;
        SetCategory(SubCategory.Weapon);
        _rightIndex = 0;
        _leftListIndex = 0;
        CancelSelection();
        _navCooldown = 0f;
        PlayerManager.Instance.MenuOpen();
        RebuildAll();
    }

    public void CloseMenu()
    {
        if (playerMovement == null) return;
        PlayerManager.Instance.MenuClose();
        CancelSelection();
    }

    // ===== Update =====
    private void Update()
    {
        if (playerMovement == null || !playerMovement.isMenuOn) return;

        RebuildAll();

        // Si onglet Options/Stats actif, focus sur onglets principaux
        if ((_main == MainTab.Options || _main == MainTab.Stats) && _focus != PanelFocus.MainTabs)
        {
            if (_main != MainTab.Equipment)
                _focus = PanelFocus.MainTabs;
        }

        Vector2 move = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        HandleMoveNavigation(move);
    }


    
    private void OnPausePressed(InputAction.CallbackContext ctx) => ToggleMenu();
    private void OnConfirmPressed(InputAction.CallbackContext ctx)
    {
        if (playerMovement != null && playerMovement.isMenuOn)
            DoConfirm();
    }

    // ===== Helpers =====
    private static bool IsEmpty(ItemInstance it) => it == null || it.Definition == null || it.Quantity <= 0;
    private int GetRightVisibleCount() => _filteredIdx.Count + (_selSource == SelSource.Slot ? 1 : 0);
    private int FilteredIndexFromVisible(int visibleIndex) => (_selSource == SelSource.Slot) ? visibleIndex - 1 : visibleIndex;

    private void SetMainTab(MainTab tab)
    {
        if (_main == tab) return;
        _main = tab;
        CancelSelection();

        if (_main == MainTab.Equipment)
        {
            _focus = PanelFocus.SubTabs;
            _rightIndex = 0;
            _leftListIndex = 0;
            RebuildAll();
        }
        else
        {
            _focus = PanelFocus.MainTabs;
        }
    }

    /// <summary>
    /// Déverse le chargeur courant du joueur (lié à em.PlayerID) dans l'ammo
    /// actuellement équipée (dernier slot de em). Met ensuite currentMagazin à 0.
    /// Fait le flush AVANT toute opération d'équipement/déséquipement.
    /// </summary>
    private void FlushMagazineToEquippedAmmo(EquipmentManager em)
    {
        if (em == null) return;
        var pm = playerManager ? playerManager : PlayerManager.Instance;
        if (!pm) return;

        int pid = em.PlayerID;
        int dump = (pid == 1) ? pm.currentMagazinP1 : pm.currentMagazinP2;
        if (dump <= 0) return;

        int n = em.SlotCount;
        ItemInstance ammo = (n > 0) ? em.GetByIndex(n - 1) : null;

        if (ammo != null && ammo.Definition != null && ammo.Definition.Type == ItemType.Ammo)
        {
            ammo.Quantity += dump;
            Debug.Log($"[GameInventory] P{pid}: déverse {dump} balle(s) dans l'ammo équipée.");
        }
        else
        {
            Debug.LogWarning($"[GameInventory] P{pid}: aucune ammo équipée — {dump} balle(s) du chargeur perdu(es).");
        }

        if (pid == 1) pm.currentMagazinP1 = 0; else pm.currentMagazinP2 = 0;
    }

    // ===== Navigation =====
    private void HandleMoveNavigation(Vector2 move)
    {
        const float dead = 0.35f;
        bool h = Mathf.Abs(move.x) > dead;
        bool v = Mathf.Abs(move.y) > dead;

        if (_navCooldown > 0f) _navCooldown -= Time.unscaledDeltaTime;

        if (_focus == PanelFocus.MainTabs)
        {
            if (h && (Edge(_lastMove.x, move.x, dead) || _navCooldown <= 0f))
            {
                int dir = move.x > 0 ? 1 : -1;
                int m = (int)_main;
                m = (m + dir + 3) % 3;
                SetMainTab((MainTab)m);
                _navCooldown = _lastMove.x == 0f ? FirstRepeatDelay : NextRepeatRate;
            }
            if (v && move.y < 0 && _main == MainTab.Equipment && (Edge(_lastMove.y, move.y, dead) || _navCooldown <= 0f))
            {
                _focus = PanelFocus.SubTabs;
                _navCooldown = _lastMove.y == 0f ? FirstRepeatDelay : NextRepeatRate;
            }
        }
        else if (_focus == PanelFocus.SubTabs)
        {
            if (_main != MainTab.Equipment) { _focus = PanelFocus.MainTabs; _lastMove = move; return; }

            if (h && (Edge(_lastMove.x, move.x, dead) || _navCooldown <= 0f))
            {
                CycleCategory(move.x > 0 ? 1 : -1);
                _navCooldown = _lastMove.x == 0f ? FirstRepeatDelay : NextRepeatRate;
            }
            if (v && move.y > 0 && (Edge(_lastMove.y, move.y, dead) || _navCooldown <= 0f))
            {
                _focus = PanelFocus.MainTabs;
                _navCooldown = _lastMove.y == 0f ? FirstRepeatDelay : NextRepeatRate;
            }
            if (v && move.y < 0 && (Edge(_lastMove.y, move.y, dead) || _navCooldown <= 0f))
            {
                _focus = PanelFocus.RightInventory;
                _navCooldown = _lastMove.y == 0f ? FirstRepeatDelay : NextRepeatRate;
            }
        }
        else
        {
            if (_main != MainTab.Equipment) { _focus = PanelFocus.MainTabs; _lastMove = move; return; }

            if (v && move.y > 0 && (Edge(_lastMove.y, move.y, dead) || _navCooldown <= 0f))
            {
                _focus = PanelFocus.SubTabs;
                _navCooldown = _lastMove.y == 0f ? FirstRepeatDelay : NextRepeatRate;
                _lastMove = move;
                return;
            }

            if (h && (Edge(_lastMove.x, move.x, dead) || _navCooldown <= 0f))
            {
                _focus = (move.x > 0f) ? PanelFocus.RightInventory : PanelFocus.LeftSlots;
                _navCooldown = _lastMove.x == 0f ? FirstRepeatDelay : NextRepeatRate;
            }

            if (v && (Edge(_lastMove.y, move.y, dead) || _navCooldown <= 0f))
            {
                int s = move.y > 0 ? -1 : 1; // haut = -1
                if (_focus == PanelFocus.RightInventory)
                {
                    int count = GetRightVisibleCount();
                    if (count > 0) { _rightIndex = Mathf.Clamp(_rightIndex + s, 0, count - 1); _navCooldown = _lastMove.y == 0f ? FirstRepeatDelay : NextRepeatRate; }
                }
                else if (_focus == PanelFocus.LeftSlots && _allowedSlots.Count > 0)
                {
                    _leftListIndex = Mathf.Clamp(_leftListIndex + s, 0, _allowedSlots.Count - 1);
                    _navCooldown = _lastMove.y == 0f ? FirstRepeatDelay : NextRepeatRate;
                }
            }
        }

        _lastMove = move;
    }

    private static bool Edge(float last, float now, float dead)
    {
        bool was = Mathf.Abs(last) > dead;
        bool isNow = Mathf.Abs(now) > dead;
        return isNow && !was;
    }

    // ===== Confirm (sélection / échanges) =====
    private void DoConfirm()
    {
        if (_main != MainTab.Equipment) return;
        if (inventory == null || (equipmentManager1 == null && equipmentManager2 == null)) return;
        if (_focus == PanelFocus.MainTabs || _focus == PanelFocus.SubTabs) return;

        // 1) Pas de sélection -> on sélectionne la source
        if (_selSource == SelSource.None)
        {
            if (_focus == PanelFocus.RightInventory)
            {
                int visibleCount = GetRightVisibleCount();
                if (visibleCount == 0) return;
                if (_selSource == SelSource.Slot) return;
                _selSource = SelSource.Inventory;
                _selInvFilteredIndex = Mathf.Clamp(_rightIndex, 0, _filteredIdx.Count - 1);
            }
            else if (_focus == PanelFocus.LeftSlots)
            {
                if (_allowedSlots.Count == 0) return;
                var sr = _allowedSlots[Mathf.Clamp(_leftListIndex, 0, _allowedSlots.Count - 1)];
                var item = sr.mgr?.GetByIndex(sr.slot);
                if (IsEmpty(item)) return;
                _selSource = SelSource.Slot;
                _selSlotListIndex = Mathf.Clamp(_leftListIndex, 0, _allowedSlots.Count - 1);
            }
            return;
        }

        // 2) Anti double-tap
        if (_selSource == SelSource.Inventory && _focus == PanelFocus.RightInventory)
        { if (_rightIndex == _selInvFilteredIndex) return; }
        if (_selSource == SelSource.Slot && _focus == PanelFocus.LeftSlots)
        { if (_leftListIndex == _selSlotListIndex) return; }

        // 3) Action selon cible
        if (_focus == PanelFocus.RightInventory)
        {
            int visibleCount = GetRightVisibleCount();
            if (visibleCount == 0) { CancelSelection(); return; }

            int displayed = Mathf.Clamp(_rightIndex, 0, visibleCount - 1);

            // Ligne 0 = “Mettre dans l’inventaire” si source = Slot
            if (_selSource == SelSource.Slot && displayed == 0)
            {
                var from = _allowedSlots[Mathf.Clamp(_selSlotListIndex, 0, _allowedSlots.Count - 1)];
                var current = from.mgr.GetByIndex(from.slot);
                if (!IsEmpty(current))
                {
                    // >>> FLUSH AVANT de déplacer vers l'inventaire
                    FlushMagazineToEquippedAmmo(from.mgr);

                    if (!inventory.Add(new ItemInstance(current.Definition, current.Quantity)))
                    { Debug.LogWarning("[GameInventory] Inventaire plein — impossible de déséquiper."); CancelSelection(); return; }
                    from.mgr.UnequipAtIndex(from.slot);
                }
                CompactInventory();
                CancelSelection();
                RebuildAll();
                return;
            }

            int targetFiltered = FilteredIndexFromVisible(displayed);
            if (targetFiltered < 0 || targetFiltered >= _filteredIdx.Count) { CancelSelection(); return; }
            TrySwapToInventory(targetFiltered);
        }
        else if (_focus == PanelFocus.LeftSlots)
        {
            if (_allowedSlots.Count == 0) { CancelSelection(); return; }
            var target = _allowedSlots[Mathf.Clamp(_leftListIndex, 0, _allowedSlots.Count - 1)];
            TrySwapToSlot(target);
        }
    }

    private void TrySwapToInventory(int targetFiltered)
    {
        int targetReal = _filteredIdx[targetFiltered];

        if (_selSource == SelSource.Inventory)
        {
            // Inventaire ↔ Inventaire
            int a = _filteredIdx[Mathf.Clamp(_selInvFilteredIndex, 0, _filteredIdx.Count - 1)];
            int b = targetReal;
            if (a == b) return;

            var tmp = inventory.Items[a];
            inventory.Items[a] = inventory.Items[b];
            inventory.Items[b] = tmp;

            CompactInventory();
            CancelSelection();
            RebuildItemFilter();
            _rightIndex = Mathf.Clamp(_rightIndex, 0, Mathf.Max(0, GetRightVisibleCount() - 1));
        }
        else if (_selSource == SelSource.Slot)
        {
            // Slot (source) ↔ Inventaire (cible) : SWAP déterministe
            var from = _allowedSlots[Mathf.Clamp(_selSlotListIndex, 0, _allowedSlots.Count - 1)];

            var a = from.mgr.GetByIndex(from.slot);
            if (IsEmpty(a)) { CancelSelection(); return; }

            var b = inventory.Items[targetReal];
            if (IsEmpty(b) || !from.mgr.CanEquipAtIndex(from.slot, b))
            {
                Debug.Log("[GameInventory] Incompatible avec ce slot.");
                CancelSelection();
                return;
            }

            // >>> FLUSH AVANT de changer l'équipement du manager source
            FlushMagazineToEquippedAmmo(from.mgr);

            // b → slot, a → inventaire (à la même place)
            from.mgr.EquipAtIndex(from.slot, b);
            inventory.Items[targetReal] = a;

            CancelSelection();
            RebuildAll();
        }
    }

    private void TrySwapToSlot(SlotRef target)
    {
        if (_selSource == SelSource.Inventory)
        {
            // Inventaire ↔ Slot (P1 ou P2)
            int srcFiltered = Mathf.Clamp(_selInvFilteredIndex, 0, _filteredIdx.Count - 1);
            int srcReal = _filteredIdx[srcFiltered];
            var invItem = inventory.Items[srcReal];

            if (invItem == null || !target.mgr.CanEquipAtIndex(target.slot, invItem))
            { Debug.Log("[GameInventory] Incompatible avec le slot cible."); CancelSelection(); return; }

            // >>> FLUSH AVANT de modifier l'équipement du manager cible
            FlushMagazineToEquippedAmmo(target.mgr);

            var prev = target.mgr.EquipAtIndex(target.slot, invItem);

            if (!IsEmpty(prev)) inventory.Items[srcReal] = prev;
            else                inventory.Items.RemoveAt(srcReal);

            CompactInventory();
            CancelSelection();
            RebuildAll();
        }
        else if (_selSource == SelSource.Slot)
        {
            // Slot ↔ Slot (P1 ↔ P2) — move/swap sûr
            var from = _allowedSlots[Mathf.Clamp(_selSlotListIndex, 0, _allowedSlots.Count - 1)];
            if (from.mgr == target.mgr && from.slot == target.slot) { CancelSelection(); return; }

            var a = from.mgr.GetByIndex(from.slot);     // source
            var b = target.mgr.GetByIndex(target.slot); // cible
            if (IsEmpty(a)) { CancelSelection(); return; }

            if (!target.mgr.CanEquipAtIndex(target.slot, a))
            { Debug.Log("[GameInventory] A ne rentre pas dans la cible."); CancelSelection(); return; }

            // >>> FLUSH AVANT tout changement (les 2 côtés peuvent être impactés)
            FlushMagazineToEquippedAmmo(from.mgr);
            if (target.mgr != from.mgr) FlushMagazineToEquippedAmmo(target.mgr);

            if (IsEmpty(b))
            {
                // MOVE vers slot vide
                from.mgr.UnequipAtIndex(from.slot);
                target.mgr.EquipAtIndex(target.slot, a);
            }
            else
            {
                // SWAP direct si B peut retourner à la source, sinon B -> inventaire
                bool bFitsBack = from.mgr.CanEquipAtIndex(from.slot, b);

                if (bFitsBack)
                {
                    target.mgr.EquipAtIndex(target.slot, a); // A -> cible
                    from.mgr.EquipAtIndex(from.slot, b);     // B -> source
                }
                else
                {
                    if (inventory != null && inventory.Add(new ItemInstance(b.Definition, b.Quantity)))
                    {
                        target.mgr.EquipAtIndex(target.slot, a); // A -> cible
                        from.mgr.UnequipAtIndex(from.slot);      // source vide
                    }
                    else
                    {
                        Debug.LogWarning("[GameInventory] Échange annulé (B ne rentre pas à la source et inventaire plein).");
                        CancelSelection();
                        return;
                    }
                }
            }

            CompactInventory();
            CancelSelection();
            RebuildAll();
        }
    }

    private void CancelSelection()
    {
        _selSource = SelSource.None;
        _selInvFilteredIndex = -1;
        _selSlotListIndex = -1;
    }

    // ===== Rebuild / filtres =====
    private void RebuildAll()
    {
        CompactInventory();

        if (inventory != null && (_lastInvCount != inventory.Items.Count))
        {
            RebuildItemFilter();
            _lastInvCount = inventory.Items.Count;
        }

        int combined = (equipmentManager1 ? equipmentManager1.SlotCount : 0)
                     + (equipmentManager2 ? equipmentManager2.SlotCount : 0);
        if (_lastSlotsCombined != combined)
        {
            RebuildAllowedSlots();
            _lastSlotsCombined = combined;
        }

        _rightIndex = Mathf.Clamp(_rightIndex, 0, Mathf.Max(0, GetRightVisibleCount() - 1));
    }

    private void SetCategory(SubCategory cat)
    {
        if (_cat == cat) return;
        _cat = cat;
        _rightIndex = 0;
        _leftListIndex = 0;
        CancelSelection();
        RebuildItemFilter();
        RebuildAllowedSlots();
    }

    private void CycleCategory(int dir)
    {
        int c = (int)_cat;
        c = (c + dir + 4) % 4;
        SetCategory((SubCategory)c);
    }

    private void RebuildItemFilter()
    {
        _filteredIdx.Clear();
        if (inventory == null) return;

        ItemType? target = GetItemTypeForCategory(_cat);
        for (int i = 0; i < inventory.Items.Count; i++)
        {
            var it = inventory.Items[i];
            if (it?.Definition == null) continue;
            if (target.HasValue && it.Definition.Type == target.Value)
                _filteredIdx.Add(i);
        }
        _rightIndex = Mathf.Clamp(_rightIndex, 0, Mathf.Max(0, GetRightVisibleCount() - 1));
    }

    private void RebuildAllowedSlots()
    {
        _allowedSlots.Clear();
        AddAllowedSlotsForManager(equipmentManager1);
        AddAllowedSlotsForManager(equipmentManager2);
        _leftListIndex = Mathf.Clamp(_leftListIndex, 0, Mathf.Max(0, _allowedSlots.Count - 1));
    }

    private void AddAllowedSlotsForManager(EquipmentManager em)
    {
        if (em == null) return;
        int n = em.SlotCount;
        if (n <= 0) return;

        switch (_cat)
        {
            case SubCategory.Weapon:     if (n >= 1) _allowedSlots.Add(new SlotRef { mgr = em, slot = 0 }); break;
            case SubCategory.Ammo:       if (n >= 1) _allowedSlots.Add(new SlotRef { mgr = em, slot = n - 1 }); break;
            case SubCategory.Accessory:  if (n >= 3) for (int s = 1; s <= n - 2; s++) _allowedSlots.Add(new SlotRef { mgr = em, slot = s }); break;
            case SubCategory.Consumable: break;
        }
    }

    private static ItemType? GetItemTypeForCategory(SubCategory cat)
    {
        switch (cat)
        {
            case SubCategory.Weapon:     return ItemType.Weapon;
            case SubCategory.Accessory:  return ItemType.Accessory;
            case SubCategory.Ammo:       return ItemType.Ammo;
            case SubCategory.Consumable: return ItemType.Consumable;
        }
        return null;
    }

    // ===== IMGUI =====
    private void OnGUI()
    {
        if (playerMovement == null || !playerMovement.isMenuOn) return;

        float margin = 10f;
        float totalW = Screen.width - margin * 2f;

        _mainTabsRect = new Rect(margin, margin, totalW, 44f);
        _subTabsRect  = new Rect(margin, _mainTabsRect.yMax + 6f, totalW, 44f);

        float contentY = _subTabsRect.yMax + 8f;
        if (_main != MainTab.Equipment) contentY = _mainTabsRect.yMax + 12f;
        float contentH = Screen.height - contentY - margin;

        float leftW = totalW * 0.38f;
        float midW  = totalW * 0.30f;
        float detW  = totalW - leftW - midW - 16f;

        _leftRect   = new Rect(margin,              contentY, leftW, contentH);
        _midRect    = new Rect(_leftRect.xMax + 8f, contentY, midW,  contentH);
        _detailRect = new Rect(_midRect.xMax + 8f,  contentY, detW,  contentH);

        DrawMainTabs(_mainTabsRect);

        if (_main == MainTab.Equipment)
        {
            DrawSubTabs(_subTabsRect);
            DrawLeftSlots(_leftRect);
            DrawRightInventory(_midRect);
            DrawDetails(_detailRect);
        }
        else if (_main == MainTab.Stats)
        {
            DrawStatsPage(new Rect(margin, contentY, totalW, contentH));
        }
        else
        {
            DrawOptionsPlaceholder(new Rect(margin, contentY, totalW, contentH));
        }
    }

    private void DrawMainTabs(Rect area)
    {
        GUI.Box(area, "");
        string[] names = { "Équipement", "Stats", "Options" };

        float pad = 10f;
        float tabW = (area.width - pad * 4f) / 3f;
        float x = area.x + pad, y = area.y + pad, h = area.height - pad * 2f;

        for (int i = 0; i < 3; i++)
        {
            Rect r = new Rect(x + i * (tabW + pad), y, tabW, h);
            var prev = GUI.color;

            if ((int)_main == i) GUI.color = Color.cyan;
            else if (_focus == PanelFocus.MainTabs) GUI.color = new Color(1f, 1f, 0.5f, 1f);

            if (GUI.Button(r, names[i]))
            {
                SetMainTab((MainTab)i);
            }

            GUI.color = prev;
        }

        var hint = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, fontSize = 11 };
        GUI.Label(new Rect(area.x, area.yMax - 18f, area.width - 8f, 16f),
            _main == MainTab.Equipment
            ? (_focus == PanelFocus.MainTabs ? "G/D: changer d’onglet | Bas: sous-onglets" : "Haut: onglets principaux")
            : "G/D: changer d’onglet", hint);
    }

    private void DrawSubTabs(Rect area)
    {
        GUI.Box(area, "");
        string[] names = { "Weapon", "Accessory", "Ammo", "Consumable" };

        float pad = 8f;
        float tabW = (area.width - pad * 5f) / 4f;
        float x = area.x + pad, y = area.y + pad, h = area.height - pad * 2f;

        for (int i = 0; i < 4; i++)
        {
            Rect r = new Rect(x + i * (tabW + pad), y, tabW, h);
            Color prev = GUI.color;

            if ((int)_cat == i) GUI.color = Color.cyan;
            else if (_focus == PanelFocus.SubTabs) GUI.color = new Color(1f, 1f, 0.5f, 1f);

            if (GUI.Button(r, names[i]))
            {
                SetCategory((SubCategory)i);
                _focus = PanelFocus.RightInventory;
            }

            GUI.color = prev;
        }

        var hint = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, fontSize = 11 };
        GUI.Label(new Rect(area.x, area.yMax - 18f, area.width - 8f, 16f),
            _selSource == SelSource.None
            ? "Haut: onglets principaux | G/D: changer | Bas: contenu"
            : "Sélection active : choisis la cible (slot / inventaire)", hint);
    }

    private void DrawLeftSlots(Rect area)
    {
        string title = "Équipement P1 & P2 — " + _cat + (_selSource == SelSource.Slot ? "  (source sélectionnée)" : "");
        GUI.Box(area, title);

        if (_allowedSlots.Count == 0)
        {
            DrawCentered(area, "(Aucun slot pertinent)");
            return;
        }

        float pad = 10f;
        float y = area.y + pad + 40f;
        float lineH = 30f;

        for (int i = 0; i < _allowedSlots.Count; i++)
        {
            var sr = _allowedSlots[i];
            if (sr.mgr == null) continue;

            int pid = sr.mgr.PlayerID;
            string constraint = sr.mgr.GetConstraintNameForIndex(sr.slot);
            var item = sr.mgr.GetByIndex(sr.slot);
            string label = $"[P{pid}][{sr.slot}] {constraint} : " + (item?.Definition != null ? item.Definition.DisplayName : "(vide)");

            Rect r = new Rect(area.x + pad, y, area.width - pad * 2f, lineH);

            bool isSelectedSource = (_selSource == SelSource.Slot && i == _selSlotListIndex);
            bool isFocusRow       = (_focus == PanelFocus.LeftSlots && i == _leftListIndex);

            var prev = GUI.color;
            if (isSelectedSource) GUI.color = Color.cyan;
            else if (isFocusRow)  GUI.color = Color.yellow;

            if (GUI.Button(r, label))
            {
                _focus = PanelFocus.LeftSlots;
                _leftListIndex = i;
                DoConfirm();
            }

            GUI.color = prev;
            y += lineH + 6f;
        }

        GUI.Label(new Rect(area.x + pad, y + 6f, area.width - pad * 2f, 22f),
            "Jump/Shoot: sélectionner puis la cible (slot P1/P2 / inventaire)");
    }

    private void DrawRightInventory(Rect area)
    {
        GUI.Box(area, $"Inventaire — {_cat}" + (_selSource == SelSource.Inventory ? "  (source sélectionnée)" : ""));
        if (inventory == null)
        {
            DrawCentered(area, "(Inventory manquant)");
            return;
        }

        float pad = 10f;
        float y = area.y + pad + 40f;
        float lineH = 28f;

        int extra = (_selSource == SelSource.Slot) ? 1 : 0;
        int visibleCount = GetRightVisibleCount();

        if (extra == 1)
        {
            Rect rTop = new Rect(area.x + pad, y, area.width - pad * 2f, lineH);
            bool isFocusTop = (_focus == PanelFocus.RightInventory && _rightIndex == 0);

            var prev = GUI.color;
            if (isFocusTop) GUI.color = Color.green;

            if (GUI.Button(rTop, "[Mettre dans l'inventaire]"))
            {
                _focus = PanelFocus.RightInventory;
                _rightIndex = 0;
                DoConfirm();
            }
            GUI.color = prev;
            y += lineH + 6f;
        }

        if (_filteredIdx.Count == 0)
        {
            if (extra == 0) DrawCentered(area, "(Aucun item)");
            return;
        }

        for (int i = 0; i < _filteredIdx.Count; i++)
        {
            int invIdx = _filteredIdx[i];
            if (invIdx < 0 || invIdx >= inventory.Items.Count) continue;

            var it = inventory.Items[invIdx];
            string name = it?.Definition != null ? it.Definition.DisplayName : "(invalide)";
            if (it != null && it.Definition.Stackable && it.Quantity > 1) name += $" x{it.Quantity}";

            int visibleIndex = i + extra;
            Rect r = new Rect(area.x + pad, y, area.width - pad * 2f, lineH);

            bool isSelectedSource = (_selSource == SelSource.Inventory && i == _selInvFilteredIndex);
            bool isFocusRow       = (_focus == PanelFocus.RightInventory && visibleIndex == _rightIndex);

            var prev = GUI.color;
            if (isSelectedSource) GUI.color = Color.cyan;
            else if (isFocusRow)  GUI.color = Color.green;

            if (GUI.Button(r, name))
            {
                _focus = PanelFocus.RightInventory;
                _rightIndex = visibleIndex;
                DoConfirm();
            }

            GUI.color = prev;
            y += lineH + 6f;
        }

        GUI.Label(new Rect(area.x + pad, y + 6f, area.width - pad * 2f, 22f),
            _selSource == SelSource.Slot
            ? "Choisis un item pour swap, ou la première ligne pour déséquiper"
            : "Jump/Shoot: sélectionner puis la cible");
    }

    // ===== Détails =====
    private void DrawDetails(Rect area)
    {
        GUI.Box(area, "Détails");
        var instance = GetSelectedForDetails();
        if (instance == null || instance.Definition == null)
        {
            DrawCentered(area, "(Sélectionne un item à droite ou un slot à gauche)");
            return;
        }

        var def = instance.Definition;
        float pad = 10f;
        Rect inner = new Rect(area.x + pad, area.y + pad + 28f, area.width - pad * 2f, area.height - pad * 2f - 28f);
        _detailScroll = GUI.BeginScrollView(inner, _detailScroll, new Rect(0,0, inner.width-16f, 900f));

        float y = 0f;
        float line = 22f;

        Rect iconR = new Rect(0, y, 64, 64);
        if (def.Icon != null) DrawSprite(iconR, def.Icon);
        GUI.Label(new Rect(72, y, inner.width - 72, line), def.DisplayName, new GUIStyle(GUI.skin.label){fontStyle=FontStyle.Bold, fontSize=16});
        y += 70f;

        if (!string.IsNullOrEmpty(def.Description))
        {
            GUI.Label(new Rect(0, y, inner.width, line), "Description :"); y += line;
            var wrap = new GUIStyle(GUI.skin.label){ wordWrap = true };
            GUI.Label(new Rect(0, y, inner.width, 80f), def.Description, wrap); y += 86f;
        }

        GUI.Label(new Rect(0, y, inner.width, line), $"Type : {def.Type}"); y += line;
        if (def.Stackable) GUI.Label(new Rect(0, y, inner.width, line), $"Quantité : {instance.Quantity} / Stack max {def.MaxStack}");
        else               GUI.Label(new Rect(0, y, inner.width, line), "Non empilable");
        y += line;

        y += 6f; DrawSeparator(ref y, inner.width);

        if (def is WeaponDefinition w)
        {
            GUI.Label(new Rect(0, y, inner.width, line), "• WEAPON"); y += line;
            GUI.Label(new Rect(0, y, inner.width, line), $"Damage : {w.Damage}"); y += line;
            GUI.Label(new Rect(0, y, inner.width, line), $"Magazine : {w.currentMagazin}/{w.maxMagazin}"); y += line;
            GUI.Label(new Rect(0, y, inner.width, line), $"Pressure : {w.currentPressureLevel:F1}/{w.maxPressureLevel:F1}"); y += line;
            GUI.Label(new Rect(0, y, inner.width, line), $"Reload time (turns) : {w.reloadTime}"); y += line;
            GUI.Label(new Rect(0, y, inner.width, line), $"Ergonomy : {w.ergonomy:F2}"); y += line;
        }
        else if (def is AccessoryDefinition a)
        {
            GUI.Label(new Rect(0, y, inner.width, line), "• ACCESSORY (modificateurs)"); y += line;
            GUI.Label(new Rect(0, y, inner.width, line), $"addDamage : {Signed(a.addDamage)}"); y += line;
            GUI.Label(new Rect(0, y, inner.width, line), $"addMaxMagazin : {Signed(a.addMaxMagazin)}"); y += line;
            GUI.Label(new Rect(0, y, inner.width, line), $"multPressureLevel : x{a.multPressureLevel:F2}"); y += line;
            GUI.Label(new Rect(0, y, inner.width, line), $"addReloadTime : {Signed(a.addReloadTime)}"); y += line;
            GUI.Label(new Rect(0, y, inner.width, line), $"multErgonomic : x{a.multErgonomic:F2}"); y += line;
        }
        else if (def is AmmoDefinition am)
        {
            GUI.Label(new Rect(0, y, inner.width, line), "• AMMO (0–100)"); y += line;
            GUI.Label(new Rect(0, y, inner.width, line), $"Poison : {am.poison:0}"); y += line;
            GUI.Label(new Rect(0, y, inner.width, line), $"Sleepy : {am.spleepy:0}"); y += line;
            GUI.Label(new Rect(0, y, inner.width, line), $"Hemorrhage : {am.hemorrhage:0}"); y += line;
            GUI.Label(new Rect(0, y, inner.width, line), $"Weakness : {am.weakness:0}"); y += line;
            GUI.Label(new Rect(0, y, inner.width, line), $"Slowness : {am.slowness:0}"); y += line;
        }
        else if (def is ConsumableDefinition c)
        {
            GUI.Label(new Rect(0, y, inner.width, line), "• CONSUMABLE"); y += line;
            GUI.Label(new Rect(0, y, inner.width, line), $"Healing : +{c.healing}"); y += line;
            GUI.Label(new Rect(0, y, inner.width, line), $"Pair : {(c.pair ? "Oui" : "Non")}"); y += line;
            GUI.Label(new Rect(0, y, inner.width, line), $"Cure Poison : {(c.curePoison ? "Oui" : "Non")}"); y += line;
            GUI.Label(new Rect(0, y, inner.width, line), $"Cure Sleepy : {(c.cureSpleepy ? "Oui" : "Non")}"); y += line;
            GUI.Label(new Rect(0, y, inner.width, line), $"Cure Weakness : {(c.cureWeakness ? "Oui" : "Non")}"); y += line;
            GUI.Label(new Rect(0, y, inner.width, line), $"Cure Slowness : {(c.cureSlowness ? "Oui" : "Non")}"); y += line;
            GUI.Label(new Rect(0, y, inner.width, line), $"Cure Hemorrhage : {(c.curehemorrhage ? "Oui" : "Non")}"); y += line;
        }

        GUI.EndScrollView();
    }

    private ItemInstance GetSelectedForDetails()
    {
        if (_main != MainTab.Equipment) return null;

        if (_focus == PanelFocus.RightInventory)
        {
            int visibleCount = GetRightVisibleCount();
            if (visibleCount > 0)
            {
                int displayed = Mathf.Clamp(_rightIndex, 0, visibleCount - 1);
                int filtered = FilteredIndexFromVisible(displayed);
                if (filtered >= 0 && filtered < _filteredIdx.Count)
                {
                    int invIdx = _filteredIdx[filtered];
                    if (invIdx >= 0 && invIdx < inventory.Items.Count) return inventory.Items[invIdx];
                }
            }
        }
        if (_focus == PanelFocus.LeftSlots && _allowedSlots.Count > 0)
        {
            var sr = _allowedSlots[Mathf.Clamp(_leftListIndex, 0, _allowedSlots.Count - 1)];
            return sr.mgr.GetByIndex(sr.slot);
        }

        if (_filteredIdx.Count > 0)
        {
            int invIdx = _filteredIdx[Mathf.Clamp(_rightIndex - (_selSource == SelSource.Slot ? 1 : 0), 0, _filteredIdx.Count - 1)];
            if (invIdx >= 0 && invIdx < inventory.Items.Count) return inventory.Items[invIdx];
        }
        if (_allowedSlots.Count > 0)
        {
            var sr = _allowedSlots[Mathf.Clamp(_leftListIndex, 0, _allowedSlots.Count - 1)];
            return sr.mgr.GetByIndex(sr.slot);
        }
        return null;
    }

    // ============== STATS PAGE ==============
    private void DrawStatsPage(Rect area)
    {
        GUI.Box(area, "Stats (P1 / P2)");

        var pm = playerManager ? playerManager : PlayerManager.Instance;
        if (!pm)
        {
            DrawCentered(area, "(PlayerManager introuvable)");
            return;
        }

        float pad = 10f;
        float colGap = 12f;
        float colW = (area.width - pad * 2f - colGap) / 2f;
        float headerH = 28f;
        float rowH = 22f;

        Rect col1 = new Rect(area.x + pad, area.y + pad + headerH, colW, area.height - pad * 2f - headerH);
        Rect col2 = new Rect(col1.xMax + colGap, col1.y, colW, col1.height);

        GUI.Label(new Rect(col1.x, area.y + pad, col1.width, headerH), "Player 1", HeaderStyle());
        GUI.Label(new Rect(col2.x, area.y + pad, col2.width, headerH), "Player 2", HeaderStyle());

        DrawStatsTable(col1, true, pm);
        DrawStatsTable(col2, false, pm);
    }

    private GUIStyle HeaderStyle()
    {
        return new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 16
        };
    }

    private void DrawStatsTable(Rect area, bool isP1, PlayerManager pm)
    {
        float y = area.y;
        float lineH = 22f;

        LabelPair(area.x, ref y, area.width, lineH, "Damage",    isP1 ? pm.DamageP1Final          : pm.DamageP2Final);
        LabelPair(area.x, ref y, area.width, lineH, "MaxMag",    isP1 ? pm.MaxMagazinP1Final      : pm.MaxMagazinP2Final);
        LabelPair(area.x, ref y, area.width, lineH, "MaxPress.", isP1 ? pm.MaxPressureLevelP1Final: pm.MaxPressureLevelP2Final, isFloat:true);
        LabelPair(area.x, ref y, area.width, lineH, "Reload",    isP1 ? pm.ReloadTimeP1Final      : pm.ReloadTimeP2Final);
        LabelPair(area.x, ref y, area.width, lineH, "Ergonomy",  isP1 ? pm.ErgonomyP1Final        : pm.ErgonomyP2Final, isFloat:true);

        y += 6f; DrawSeparator(ref y, area.width);

        float poison   = isP1 ? pm.poisonP1    : pm.poisonP2;
        float sleepy   = isP1 ? pm.spleepyP1   : pm.spleepyP2;
        float hemorr   = isP1 ? pm.hemorrhageP1: pm.hemorrhageP2;
        float weak     = isP1 ? pm.weaknessP1  : pm.weaknessP2;
        float slow     = isP1 ? pm.slownessP1  : pm.slownessP2;

        if (poison != 0f)   LabelPair(area.x, ref y, area.width, lineH, "Poison",     poison, isFloat:true, suffix:"%");
        if (sleepy != 0f)   LabelPair(area.x, ref y, area.width, lineH, "Sleepy",     sleepy, isFloat:true, suffix:"%");
        if (hemorr != 0f)   LabelPair(area.x, ref y, area.width, lineH, "Hemorrhage", hemorr, isFloat:true, suffix:"%");
        if (weak != 0f)     LabelPair(area.x, ref y, area.width, lineH, "Weakness",   weak,   isFloat:true, suffix:"%");
        if (slow != 0f)     LabelPair(area.x, ref y, area.width, lineH, "Slowness",   slow,   isFloat:true, suffix:"%");
    }

    private void LabelPair(float x, ref float y, float width, float h, string label, float value, bool isFloat=false, string suffix="")
    {
        float labelW = width * 0.55f;
        Rect rLabel = new Rect(x, y, labelW, h);
        Rect rVal   = new Rect(x + labelW + 4f, y, width - labelW - 4f, h);
        GUI.Label(rLabel, label + " :");
        GUI.Label(rVal, isFloat ? value.ToString("0.##") + suffix : ((int)value).ToString() + suffix);
        y += h + 2f;
    }

    // ============== Options placeholder ==============
    private void DrawOptionsPlaceholder(Rect area)
    {
        GUI.Box(area, "Options");
        DrawCentered(new Rect(area.x, area.y, area.width, area.height), "(Options à venir)");
    }

    // ===== Utils UI =====
    private static void DrawSeparator(ref float y, float width)
    {
        var prev = GUI.color; GUI.color = new Color(1,1,1,0.25f);
        GUI.Box(new Rect(0, y, width, 1f), GUIContent.none);
        GUI.color = prev; y += 6f;
    }
    private static string Signed(int v) => v > 0 ? "+" + v : v.ToString();
    private static void DrawCentered(Rect area, string text)
    { GUI.Label(area, text, new GUIStyle(GUI.skin.label){ alignment = TextAnchor.MiddleCenter }); }
    private static void DrawSprite(Rect r, Sprite s)
    {
        if (s == null || s.texture == null) return;
        float ratio = s.rect.width / s.rect.height;
        float targetW = r.height * ratio;
        Rect tr = new Rect(r.x, r.y, Mathf.Min(targetW, r.width), r.height);
        Rect uv = new Rect(s.rect.x / s.texture.width, s.rect.y / s.texture.height, s.rect.width / s.texture.width, s.rect.height / s.texture.height);
        GUI.DrawTextureWithTexCoords(tr, s.texture, uv);
    }

    // ===== Maintenance inventaire =====
    private void CompactInventory()
    {
        if (inventory == null) return;
        for (int i = inventory.Items.Count - 1; i >= 0; i--)
        {
            var it = inventory.Items[i];
            if (it == null || it.Definition == null || it.Quantity <= 0)
                inventory.Items.RemoveAt(i);
        }
    }
}
