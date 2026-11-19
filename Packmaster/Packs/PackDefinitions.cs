using Packmaster.Cards.CoreSet;

namespace Packmaster.Packs;

public static class PackDefinitions
{
    public const string CoreSetId = "CoreSet";

    public static void RegisterAll()
    {
        PackRegistry.Register(new PackDefinition(
            Id: CoreSetId,
            AlwaysInclude: true,
            CardTypes:
            [
                typeof(BackpackSmack),
                typeof(BoosterTutor),
                typeof(DestinyDraw),
                typeof(Flick),
                typeof(MayhemForm),
                typeof(RippedPecs),
                typeof(Showoff),
                typeof(Sideboard),
                typeof(SmithingHammer),
                typeof(Synergize),
            ]));
    }
}
