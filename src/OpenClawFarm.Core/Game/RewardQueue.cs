using OpenClawFarm.Core.Models;

namespace OpenClawFarm.Core.Game;

public sealed class RewardQueue
{
    private readonly List<PendingReward> _pending = [];
    private int _counter;

    public IReadOnlyList<PendingReward> Pending => _pending;

    public void Grant(string source, string description, int gold = 0, Dictionary<string, int>? items = null)
    {
        _pending.Add(new PendingReward(
            $"reward_{++_counter}",
            source,
            description,
            items ?? new Dictionary<string, int>(),
            gold,
            false));
    }

    public (bool Ok, string Message, int Gold, Dictionary<string, int> Items) Claim(string? rewardId, Inventory inventory)
    {
        var toClaim = rewardId == null
            ? _pending.Where(r => !r.Claimed).ToList()
            : _pending.Where(r => r.Id == rewardId && !r.Claimed).ToList();

        if (toClaim.Count == 0)
            return (false, rewardId == null ? "no pending rewards" : "reward not found or already claimed", 0, new());

        var totalGold = 0;
        var items = new Dictionary<string, int>();
        foreach (var r in toClaim)
        {
            totalGold += r.Gold;
            inventory.AddGold(r.Gold);
            foreach (var (itemId, count) in r.Items)
            {
                inventory.AddItem(itemId, count);
                items[itemId] = items.GetValueOrDefault(itemId) + count;
            }
            var idx = _pending.IndexOf(r);
            _pending[idx] = r with { Claimed = true };
        }

        return (true, $"claimed {toClaim.Count} reward(s)", totalGold, items);
    }

    public RewardState ToState() =>
        new(_pending.Where(r => !r.Claimed).ToList(), _pending.Count(r => !r.Claimed));
}
