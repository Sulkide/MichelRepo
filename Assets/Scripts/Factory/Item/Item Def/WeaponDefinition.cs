using UnityEngine;

[CreateAssetMenu(menuName = "RPG/Items/Weapon", fileName = "NewWeapon")]
public class WeaponDefinition : ItemDefinition
{
    public EquipmentSlot Slot = EquipmentSlot.Weapon;

    [Header("Weapon Stats (exactement celles demandées)")]
    public int Damage;                 // dégâts infligés
    public int maxMagazin;             // nb max de balles dans la chambre
    public int currentMagazin;         // nb actuel de balles dans la chambre
    public float maxPressureLevel;     // temps max avant indisponibilité (temps IRL)
    public float currentPressureLevel; // pression restante
    public int reloadTime;             // nombre de tours perdus lors du rechargement
    public float ergonomy;             // fenêtre de tir disponible en combat

    private void OnValidate()
    {
        Type = ItemType.Weapon;
        Stackable = false;
        MaxStack = 1;

        // bornes de sécurité simples
        if (maxMagazin < 0) maxMagazin = 0;
        if (currentMagazin < 0) currentMagazin = 0;
        if (currentMagazin > maxMagazin) currentMagazin = maxMagazin;
        if (maxPressureLevel < 0f) maxPressureLevel = 0f;
        if (currentPressureLevel < 0f) currentPressureLevel = 0f;
        if (currentPressureLevel > maxPressureLevel) currentPressureLevel = maxPressureLevel;
        if (reloadTime < 0) reloadTime = 0;
        if (ergonomy < 0f) ergonomy = 0f;
    }
}