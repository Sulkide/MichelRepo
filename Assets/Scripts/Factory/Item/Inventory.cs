using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    public int Capacity = 20;
    public List<ItemInstance> Items = new List<ItemInstance>();
    
    public bool Add(ItemInstance toAdd)
    {
        if (toAdd == null || toAdd.Definition == null) return false;

        if (toAdd.Definition.Stackable)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                var slot = Items[i];
                if (slot != null && slot.Definition == toAdd.Definition && slot.Quantity < slot.Definition.MaxStack)
                {
                    int espace = slot.Definition.MaxStack - slot.Quantity;
                    int move = toAdd.Quantity <= espace ? toAdd.Quantity : espace;

                    slot.Quantity += move;
                    toAdd.Quantity -= move;

                    if (toAdd.Quantity <= 0) return true;
                }
            }
        }

        if (Items.Count >= Capacity) return false;
        Items.Add(new ItemInstance(toAdd.Definition, toAdd.Quantity));
        return true;
    }

    public bool RemoveById(string id, int quantity = 1)
    {
        if (string.IsNullOrEmpty(id)) return false;

        for (int i = 0; i < Items.Count; i++)
        {
            var slot = Items[i];
            if (slot != null && slot.Definition != null && slot.Definition.Id == id)
            {
                if (slot.Quantity > quantity)
                {
                    slot.Quantity -= quantity;
                    return true;
                }
                else
                {
                    Items.RemoveAt(i);
                    return true;
                }
            }
        }
        return false;
    }
}