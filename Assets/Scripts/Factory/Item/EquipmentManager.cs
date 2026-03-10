using System;
using UnityEngine;

public class EquipmentManager : MonoBehaviour
{
    [Header("Player")]
    public int PlayerID;
    public PlayerMovement playerMovement;
    private void Awake()
    {
        if (playerMovement == null)
        {
            playerMovement = GetComponentInParent<PlayerMovement>();
        }

        PlayerID = playerMovement.currentId;
    }

    [SerializeField] private ItemInstance[] slots = new ItemInstance[4];
    public int SlotCount => slots != null ? slots.Length : 0;

    public ItemInstance Get(EquipmentSlot slot) { return slots[(int)slot]; }
    
    public ItemInstance GetByIndex(int index)
    {
        if (slots == null || index < 0 || index >= slots.Length) return null;
        return slots[index];
    }

    public bool CanEquipAtIndex(int index, ItemInstance item)
    {
        if (item == null || item.Definition == null) return false;
        if (slots == null || index < 0 || index >= slots.Length) return false;

        int last = slots.Length - 1;
        var t = item.Definition.Type;

        if (index == 0)            return t == ItemType.Weapon;
        if (index == last)         return t == ItemType.Ammo;
        return t == ItemType.Accessory;
    }

    public string GetConstraintNameForIndex(int index)
    {
        if (slots == null || index < 0 || index >= slots.Length) return "?";
        int last = slots.Length - 1;
        if (index == 0) return "Weapon";
        if (index == last) return "Ammo";
        return "Accessory";
    }

    public ItemInstance EquipAtIndex(int index, ItemInstance item)
    {
        if (!CanEquipAtIndex(index, item)) return null;
   
        var prev = slots[index];
        slots[index] = item;
        return prev;
    }
    
    public ItemInstance Equip(ItemInstance item)
    {
        if (item == null || item.Definition == null) return null;
        
        if (item.Definition.Type == ItemType.Weapon) return EquipAtIndex(0, item);
        
        return null;
    }

    public ItemInstance UnequipAtIndex(int index)
    {
        if (slots == null || index < 0 || index >= slots.Length) return null;
        var prev = slots[index];
        slots[index] = null;
        return prev;
    }

    public ItemInstance Unequip(EquipmentSlot slot)
    {
        int index = (int)slot;
        return UnequipAtIndex(index);
    }
}

