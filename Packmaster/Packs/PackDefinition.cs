using MegaCrit.Sts2.Core.Localization;

namespace Packmaster.Packs;

public sealed record PackDefinition(
    string Id,
    bool AlwaysInclude,
    IReadOnlyList<Type> CardTypes)
{
    private string KeyPrefix => $"PACKMASTER-PACK_{Id.ToUpperInvariant()}";

    public LocString Title => new("gameplay_ui", $"{KeyPrefix}.title");
    public LocString Description => new("gameplay_ui", $"{KeyPrefix}.description");
}
