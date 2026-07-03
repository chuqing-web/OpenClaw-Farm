using OpenClawFarm.Core.Models;

namespace OpenClawFarm.Core.Game;

public sealed class Inventory
{
    public int Gold { get; private set; }
    private readonly Dictionary<string, int> _items = new();

    public Inventory(int gold = 50, IEnumerable<BagItem>? initialItems = null)
    {
        Gold = gold;
        if (initialItems != null)
        {
            foreach (var item in initialItems)
                _items[item.ItemId] = item.Count;
        }
    }

    public BagState GetBag() => new(Gold,
        _items.Where(kv => kv.Value > 0).Select(kv => new BagItem(kv.Key, kv.Value)).ToList());

    public int GetCount(string itemId) => _items.GetValueOrDefault(itemId);

    public const int MaxStackPerItem = 500;

    public bool TryAddItem(string itemId, int count)
    {
        if (count <= 0) return true;
        if (GetCount(itemId) + count > MaxStackPerItem) return false;
        AddItem(itemId, count);
        return true;
    }

    public void AddItem(string itemId, int count)
    {
        if (count <= 0) return;
        _items[itemId] = GetCount(itemId) + count;
    }

    public bool RemoveItem(string itemId, int count)
    {
        var current = GetCount(itemId);
        if (current < count) return false;
        var next = current - count;
        if (next == 0) _items.Remove(itemId);
        else _items[itemId] = next;
        return true;
    }

    public void AddGold(int amount) => Gold += amount;

    public void SetGold(int amount) => Gold = Math.Max(0, amount);

    public void LoadFrom(int gold, Dictionary<string, int> items)
    {
        Gold = gold;
        _items.Clear();
        foreach (var (id, count) in items)
            if (count > 0) _items[id] = count;
    }
}
