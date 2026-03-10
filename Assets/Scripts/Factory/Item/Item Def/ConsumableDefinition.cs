using UnityEngine;

[CreateAssetMenu(menuName = "RPG/Items/Consumable", fileName = "NewConsumable")]

public class ConsumableDefinition : ItemDefinition
{
    [Header("Effets")]
    public int healing;
    public bool pair;            // agit sur deux persos en même temps
    public bool curePoison;
    public bool cureSpleepy;
    public bool cureWeakness;
    public bool cureSlowness;
    public bool curehemorrhage;

    private void OnValidate()
    {
        Type = ItemType.Consumable;
        Stackable = true;
        if (MaxStack < 1) MaxStack = 99;

        if (healing < 0) healing = 0;
    }
}