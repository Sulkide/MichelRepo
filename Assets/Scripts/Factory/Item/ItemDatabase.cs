using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "RPG/Items/Database", fileName = "ItemDatabase")]
public class ItemDatabase : ScriptableObject
{
    public List<ItemDefinition> Items = new List<ItemDefinition>();

    private Dictionary<string, ItemDefinition> _lookup;

    public void RebuildLookup()
    {
        _lookup = new Dictionary<string, ItemDefinition>();
        for (int i = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            if (item == null || string.IsNullOrEmpty(item.Id)) continue;
            if (!_lookup.ContainsKey(item.Id))
                _lookup.Add(item.Id, item);
        }
    }

    public ItemDefinition GetById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (_lookup == null) RebuildLookup();

        ItemDefinition def;
        if (_lookup.TryGetValue(id, out def))
            return def;

        return null;
    }
}