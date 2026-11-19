using MegaCrit.Sts2.Core.Models;

namespace Packmaster.Packs;

public static class PackRegistry
{
    private static readonly List<PackDefinition> _packs = [];
    private static readonly Dictionary<string, PackDefinition> _byId = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<Type, PackDefinition> _byCardType = [];

    public static IReadOnlyList<PackDefinition> AllPacks => _packs;

    public static void Register(PackDefinition pack)
    {
        if (_byId.ContainsKey(pack.Id))
            throw new InvalidOperationException($"Duplicate Packmaster pack ID: {pack.Id}");

        var seenInPack = new HashSet<Type>();
        foreach (var cardType in pack.CardTypes)
        {
            if (!seenInPack.Add(cardType))
                throw new InvalidOperationException($"Card type {cardType.Name} is listed twice in pack {pack.Id}");
            if (_byCardType.TryGetValue(cardType, out var existing))
                throw new InvalidOperationException($"Card type {cardType.Name} is already registered to pack {existing.Id}");
        }

        _byId.Add(pack.Id, pack);
        foreach (var cardType in pack.CardTypes)
            _byCardType.Add(cardType, pack);
        _packs.Add(pack);
    }

    public static PackDefinition? Get(string id) => _byId.GetValueOrDefault(id);

    public static PackDefinition? GetForCard(CardModel card) => _byCardType.GetValueOrDefault(card.GetType());

    internal static IDisposable OverrideForTests(IEnumerable<PackDefinition> packs)
    {
        var saved = _packs.ToList();
        ReplaceAll(packs);
        PackRunState.Reset();
        return new RestoreScope(saved);
    }

    private static void ReplaceAll(IEnumerable<PackDefinition> packs)
    {
        _packs.Clear();
        _byId.Clear();
        _byCardType.Clear();
        foreach (var pack in packs)
            Register(pack);
    }

    private sealed class RestoreScope(List<PackDefinition> saved) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ReplaceAll(saved);
            PackRunState.Reset();
        }
    }
}
