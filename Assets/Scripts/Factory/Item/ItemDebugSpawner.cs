using UnityEngine;

public class ItemDebugSpawner : MonoBehaviour
{
    public Inventory Inventory;
    public EquipmentManager EquipMgr;

    private void Start()
    {
        var potion = ItemFactory.Instance.Create("Consimable TEST 01 ID", 3);
        var sword  = ItemFactory.Instance.Create("Weapon TEST 01 ID", 1);

        if (Inventory != null)
        {
            Inventory.Add(potion);
            Inventory.Add(sword);
        }

        /*if (EquipMgr != null)
        {
            // équipe l’épée si présente en inventaire
            // (dans un vrai flux, on chercherait le slot correspondant dans l’inventaire)
            var equipped = EquipMgr.Equip(sword);
            if (equipped != null && Inventory != null) Inventory.Add(equipped);
        }*/
    }
}