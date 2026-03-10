using UnityEngine;

[CreateAssetMenu(menuName = "RPG/Items/Accessory", fileName = "NewAccessory")]
public class AccessoryDefinition : ItemDefinition
{
    public EquipmentSlot Slot = EquipmentSlot.Accessory;

    [Header("Accessory Modifiers")]
    public int addDamage;
    public int addMaxMagazin;
    public float multPressureLevel;
    public int addReloadTime;
    public float multErgonomic;

    private void OnValidate()
    {
        Type = ItemType.Accessory;
        Stackable = false;
        MaxStack = 1;
        
        if (multPressureLevel < 0f) multPressureLevel = 0f;
        if (multErgonomic < 0f) multErgonomic = 0f;
    }
}