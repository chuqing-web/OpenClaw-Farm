using OpenClawFarm.Core.Models;

namespace OpenClawFarm.Core.Game;

public sealed class LivestockManager
{
    private readonly List<Animal> _animals =
    [
        new("animal_01", "chicken", 18, 14),
        new("animal_02", "chicken", 20, 14),
        new("animal_03", "cow", 22, 16),
    ];

    public int BarnLevel { get; private set; } = 1;

    public IReadOnlyList<Animal> Animals => _animals;

    public void Tick()
    {
        foreach (var a in _animals)
            a.Tick();
    }

    public (bool Ok, string Message) Feed(string animalId, string itemId, Inventory inv)
    {
        var animal = _animals.FirstOrDefault(a => a.Id == animalId);
        if (animal == null) return (false, "unknown animal");

        var feedItem = animal.Type switch
        {
            "chicken" => ItemIds.CropWheat,
            "cow" => ItemIds.CropCorn,
            _ => null,
        };

        if (itemId != feedItem && itemId != "feed")
            return (false, $"use {feedItem} to feed {animal.Type}");

        if (inv.GetCount(feedItem!) < 1)
            return (false, $"no {feedItem} in bag");

        inv.RemoveItem(feedItem!, 1);
        animal.Feed();
        return (true, $"fed {animalId}");
    }

    public (bool Ok, string Message, string? ProductId, int Count) Collect(string animalId, Inventory inv)
    {
        var animal = _animals.FirstOrDefault(a => a.Id == animalId);
        if (animal == null) return (false, "unknown animal", null, 0);
        if (!animal.HasProduct)
            return (false, "no product ready", null, 0);

        var (productId, count) = animal.Collect();
        inv.AddItem(productId, count);
        return (true, $"collected {count}x {productId}", productId, count);
    }

    public LivestockState ToState() =>
        new(_animals.Select(a => a.ToState()).ToList(), BarnLevel);

    public List<AnimalSaveData> Export() =>
        _animals.Select(a => new AnimalSaveData(a.Id, a.Hunger, a.Happiness, a.HasProduct)).ToList();

    public void Restore(List<AnimalSaveData> data)
    {
        foreach (var d in data)
            _animals.FirstOrDefault(a => a.Id == d.Id)?.Restore(d);
    }

    public sealed class Animal
    {
        public string Id { get; }
        public string Type { get; }
        public int X { get; }
        public int Y { get; }
        public int Hunger { get; private set; } = 50;
        public int Happiness { get; private set; } = 80;
        public bool HasProduct { get; private set; }
        public string ProductId => Type switch
        {
            "chicken" => ItemIds.CropEgg,
            "cow" => ItemIds.CropMilk,
            _ => ItemIds.CropWool,
        };

        public Animal(string id, string type, int tileX, int tileY)
        {
            Id = id;
            Type = type;
            (X, Y) = ItemIds.TileToPixel(tileX, tileY);
        }

        public void Feed()
        {
            Hunger = Math.Min(100, Hunger + 30);
            Happiness = Math.Min(100, Happiness + 10);
        }

        public void AutoFeed() => Feed();

        public void Starve()
        {
            Hunger = Math.Max(0, Hunger - 20);
            Happiness = Math.Max(0, Happiness - 15);
            HasProduct = false;
        }

        public void Tick()
        {
            Hunger = Math.Max(0, Hunger - 8);
            if (Hunger > 40 && !HasProduct)
                HasProduct = Random.Shared.NextDouble() < 0.35;
            if (Hunger <= 0) Happiness = Math.Max(0, Happiness - 5);
        }

        public (string ProductId, int Count) Collect()
        {
            HasProduct = false;
            return (ProductId, Type == "chicken" ? 1 + Random.Shared.Next(0, 2) : 1);
        }

        public AnimalState ToState() => new(
            Id, Type, X, Y, Hunger, Happiness, HasProduct, ProductId,
            HasProduct ? 1 : 0, Happiness >= 70);

        public void Restore(AnimalSaveData d)
        {
            Hunger = d.Hunger;
            Happiness = d.Happiness;
            HasProduct = d.HasProduct;
        }
    }
}
