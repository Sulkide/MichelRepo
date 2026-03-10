using System.Text;
using TMPro;
using UnityEngine;

public class PlayerHudStatsTMP : MonoBehaviour
{
    [SerializeField] private PlayerMovement player;
    [SerializeField] private PlayerStatusController statusController;
    [SerializeField] private TMP_Text playerLifeText;
    [SerializeField] private TMP_Text playerIdText;
    [SerializeField] private TMP_Text stateText;       
    [SerializeField] private TMP_Text weaponStatsText;
    [SerializeField] private TMP_Text ammoText;
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.15f;

    private float _t;
    private bool _hiddenForNonOwner;

    private void Awake()
    {
        statusController = player.GetComponent<PlayerStatusController>();
    }

    private void Update()
    {
        if (player == null) return;
        if (!_hiddenForNonOwner && player.NetworkObject != null && player.NetworkObject.IsSpawned)
        {
            if (!player.IsOwner)
            {
                gameObject.SetActive(false);
                _hiddenForNonOwner = true;
                return;
            }
        }

        _t -= Time.unscaledDeltaTime;
        if (_t > 0f) return;
        _t = refreshInterval;

        RefreshTexts();
    }

    private void RefreshTexts()
    {
        int id = player.currentId;

        if (playerIdText != null) playerIdText.text = $"Player ID : {id}";

        if (stateText != null)
        {
            if (statusController != null)
                stateText.text = $"State : {statusController.CurrentType}";
            else
                stateText.text = "State : (aucun status controller)";
        }

        if (playerLifeText != null)
        {
            if (player != null)
                playerLifeText.text = $"Life : {player.currentHealth.Value}/{player.maxHealth}";
            else
            {
                playerLifeText.text = $"Life : pas de vie référencer";
            }
                
        }

        var pm = PlayerManager.Instance;
        if (pm == null)
        {
            if (weaponStatsText != null) weaponStatsText.text = "Weapon : (PlayerManager missing)";
            if (ammoText != null) ammoText.text = "Ammo : (PlayerManager missing)";
            return;
        }

        int dmg, maxMag, reload;
        float maxPress, ergo;

        if (id == 1)
        {
            dmg = pm.DamageP1Final;
            maxMag = pm.MaxMagazinP1Final;
            maxPress = pm.MaxPressureLevelP1Final;
            reload = pm.ReloadTimeP1Final;
            ergo = pm.ErgonomyP1Final;
        }
        else if (id == 2)
        {
            dmg = pm.DamageP2Final;
            maxMag = pm.MaxMagazinP2Final;
            maxPress = pm.MaxPressureLevelP2Final;
            reload = pm.ReloadTimeP2Final;
            ergo = pm.ErgonomyP2Final;
        }
        else
        {
            dmg = 0; maxMag = 0; maxPress = 0; reload = 0; ergo = 0;
        }

        if (weaponStatsText != null)
        {
            weaponStatsText.text =
                $"Weapon Stats\n" +
                $"Damage : {dmg}\n" +
                $"MaxMag : {maxMag}\n" +
                $"MaxPress : {maxPress:0.##}\n" +
                $"Reload : {reload}\n" +
                $"Ergonomy : {ergo:0.##}";
        }
        
        int magCurrent = (id == 1) ? pm.currentMagazinP1 : pm.currentMagazinP2;

        string reserveInfo = GetEquippedAmmoInfo(player);

        if (ammoText != null)
        {
            ammoText.text =
                $"Ammo\n" +
                $"Magazine : {magCurrent}/{maxMag}\n" +
                reserveInfo;
        }
    }

    private string GetEquippedAmmoInfo(PlayerMovement pmove)
    {
        if (pmove == null || pmove.equipementManager == null)
            return "Equipped Ammo : (no equipment manager)";

        var em = pmove.equipementManager;

        int n = em.SlotCount;
        if (n <= 0) return "Equipped Ammo : none";

        ItemInstance ammo = em.GetByIndex(n - 1);
        if (ammo == null || ammo.Definition == null)
            return "Equipped Ammo : none";

        int qty = ammo.Quantity;
        int maxStack = ammo.Definition.MaxStack;

        var sb = new StringBuilder();
        sb.Append("Equipped Ammo : ");
        sb.Append(ammo.Definition.name);

        if (maxStack > 0) sb.Append($" ({qty}/{maxStack})");
        else sb.Append($" ({qty})");

        return sb.ToString();
    }
}
