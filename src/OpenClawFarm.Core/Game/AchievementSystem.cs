using OpenClawFarm.Core.Models;

namespace OpenClawFarm.Core.Game;

public sealed class AchievementSystem
{
    private readonly Dictionary<string, AchievementEntry> _achievements = new();

    public AchievementSystem()
    {
        Define("harvest_100", "百收达人", "farm", 100, "收割100次作物");
        Define("gold_10k", "万元户", "economy", 10_000, "累计赚取1万金币");
        Define("gold_100k", "十万大亨", "economy", 100_000, "累计赚取10万金币");
        Define("order_daily", "订单新手", "order", 1, "完成1个日常订单");
        Define("order_contract", "合约商人", "order", 1, "完成1个七日合约");
        Define("process_first", "加工入门", "craft", 1, "首次加工成品");
        Define("livestock_feed", "畜牧新手", "livestock", 5, "投喂动物5次");
        Define("auto_48h", "自动化霸主", "auto", 48, "AI托管48小时(模拟:2000次动作)");
        Define("mastery_crop", "作物精通I", "mastery", 100, "单作物收割100次");
        Define("prestige_1", "初次转生", "prestige", 1, "完成首次转生");
        Define("victory_main", "田园大亨", "victory", 1, "达成主线通关");
        Define("victory_perfect", "农场传奇", "victory", 1, "达成完美毕业");
    }

    private void Define(string id, string name, string category, int target, string rewardDesc) =>
        _achievements[id] = new AchievementEntry(id, name, category, 0, target, false, false, rewardDesc);

    public void Increment(string id, int amount = 1)
    {
        if (!_achievements.TryGetValue(id, out var a)) return;
        var progress = Math.Min(a.Target, a.Progress + amount);
        var unlocked = progress >= a.Target;
        _achievements[id] = a with { Progress = progress, Unlocked = unlocked || a.Unlocked };
    }

    public void SetProgress(string id, int value)
    {
        if (!_achievements.TryGetValue(id, out var a)) return;
        var progress = Math.Min(a.Target, value);
        _achievements[id] = a with { Progress = progress, Unlocked = progress >= a.Target || a.Unlocked };
    }

    public AchievementState ToState()
    {
        var list = _achievements.Values.OrderBy(a => a.Category).ThenBy(a => a.Id).ToList();
        return new AchievementState(list, list.Count(a => a.Unlocked), list.Count);
    }

    public IEnumerable<AchievementEntry> Unclaimed() =>
        _achievements.Values.Where(a => a.Unlocked && !a.RewardClaimed);

    public bool MarkClaimed(string id)
    {
        if (!_achievements.TryGetValue(id, out var a) || !a.Unlocked) return false;
        _achievements[id] = a with { RewardClaimed = true };
        return true;
    }

    public List<AchievementSaveData> Export() =>
        _achievements.Values.Select(a => new AchievementSaveData(a.Id, a.Progress, a.Unlocked, a.RewardClaimed)).ToList();

    public void Restore(List<AchievementSaveData> data)
    {
        foreach (var key in _achievements.Keys.ToList())
        {
            var a = _achievements[key];
            _achievements[key] = a with { Progress = 0, Unlocked = false, RewardClaimed = false };
        }
        foreach (var d in data)
        {
            if (!_achievements.TryGetValue(d.Id, out var a)) continue;
            _achievements[d.Id] = a with { Progress = d.Progress, Unlocked = d.Unlocked, RewardClaimed = d.RewardClaimed };
        }
    }
}
