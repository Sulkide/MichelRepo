using System;
using UnityEngine;

[Serializable]
public class ItemInstance
{
    public ItemDefinition Definition;
    public int Quantity;

    public ItemInstance(ItemDefinition def, int qty)
    {
        Definition = def;
        Quantity = qty < 1 ? 1 : qty;
        if (!def.Stackable) Quantity = 1;
        if (def.Stackable && Quantity > def.MaxStack) Quantity = def.MaxStack;
    }
}