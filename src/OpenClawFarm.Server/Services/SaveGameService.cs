using System.Text.Json;
using OpenClawFarm.Core.Game;
using OpenClawFarm.Core.Models;

namespace OpenClawFarm.Server.Services;

public sealed class SaveGameService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly GameWorld _world;
    private readonly string _savePath;
    private readonly object _fileLock = new();

    public SaveGameService(GameWorld world)
    {
        _world = world;
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenClawFarm");
        Directory.CreateDirectory(dir);
        _savePath = Path.Combine(dir, "save.json");
        _world.OnAutoSave += () => Save();
    }

    public SaveInfo GetInfo()
    {
        lock (_fileLock)
        {
            if (!File.Exists(_savePath))
                return new SaveInfo(false, null, null, null, null);

            try
            {
                var save = LoadFromDisk();
                if (save == null)
                    return new SaveInfo(false, null, null, null, null);

                var season = new SeasonSystem();
                season.Restore(save.GameDay);
                return new SaveInfo(true, save.SavedAt, save.Gold, save.GameDay, season.CurrentSeason);
            }
            catch
            {
                return new SaveInfo(false, null, null, null, null);
            }
        }
    }

    public bool HasSave()
    {
        lock (_fileLock)
            return File.Exists(_savePath);
    }

    public void Save()
    {
        if (!_world.SessionActive) return;
        lock (_fileLock)
        {
            var data = _world.ExportSave();
            var json = JsonSerializer.Serialize(data, JsonOptions);
            File.WriteAllText(_savePath, json);
        }
    }

    public FarmSaveData? Load()
    {
        lock (_fileLock)
            return LoadFromDisk();
    }

    public void Delete()
    {
        lock (_fileLock)
        {
            if (File.Exists(_savePath))
                File.Delete(_savePath);
        }
    }

    private FarmSaveData? LoadFromDisk()
    {
        if (!File.Exists(_savePath)) return null;
        var json = File.ReadAllText(_savePath);
        return JsonSerializer.Deserialize<FarmSaveData>(json, JsonOptions);
    }
}
