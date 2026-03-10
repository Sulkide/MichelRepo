using UnityEngine;

[CreateAssetMenu(menuName = "RPG/Items/Ammo", fileName = "NewAmmo")]
public class AmmoDefinition : ItemDefinition
{
    [Header("Ammo Effects (0..100)")]
    [Range(0,100)] public float poison;
    [Range(0,100)] public float spleepy;
    [Range(0,100)] public float hemorrhage;
    [Range(0,100)] public float weakness;
    [Range(0,100)] public float slowness;

    private void OnValidate()
    {
        Type = ItemType.Ammo;
        Stackable = true;
        if (MaxStack < 1) MaxStack = 99;
        
        poison = Mathf.Clamp(poison, 0f, 100f);
        spleepy = Mathf.Clamp(spleepy, 0f, 100f);
        hemorrhage = Mathf.Clamp(hemorrhage, 0f, 100f);
        weakness = Mathf.Clamp(weakness, 0f, 100f);
        slowness = Mathf.Clamp(slowness, 0f, 100f);
    }
}