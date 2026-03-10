using UnityEngine;

public enum ItemType
{
    Weapon,
    Consumable,
    Accessory,
    Ammo
}

public enum EquipmentSlot
{
    Weapon,
    Head,
    Body,
    Accessory
}

public abstract class ItemDefinition : ScriptableObject
{
    [Header("Identité")]
    public string Id;                       
    public string DisplayName = "New Item";
    [TextArea] public string Description;
    public Sprite Icon;
    public ItemType Type;

    [Header("Stack")]
    public bool Stackable = false;
    public int MaxStack = 1;
}

