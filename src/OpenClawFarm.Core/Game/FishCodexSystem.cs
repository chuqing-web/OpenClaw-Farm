using OpenClawFarm.Core.Models;

namespace OpenClawFarm.Core.Game;

public sealed class FishCodexSystem
{
    private static readonly string[] AllFish =
        [ItemIds.FishCommon, ItemIds.FishMedium, ItemIds.FishRare, ItemIds.FishGlow];

    private readonly HashSet<string> _discovered = [];

    public void OnCatch(string fishId) => _discovered.Add(fishId);

    public FishCodexState ToState() => new(
        AllFish.Select(f => new FishCodexEntry(f, _discovered.Contains(f))).ToList(),
        _discovered.Count,
        AllFish.Length);

    public FishCodexSaveData Export() => new(_discovered.ToList());

    public void Restore(FishCodexSaveData? data)
    {
        _discovered.Clear();
        if (data == null) return;
        foreach (var f in data.Discovered) _discovered.Add(f);
    }
}
