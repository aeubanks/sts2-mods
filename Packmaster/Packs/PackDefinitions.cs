using Packmaster.Cards.BitingCold;
using Packmaster.Cards.CoreSet;
using Packmaster.Cards.Quiet;

namespace Packmaster.Packs;

public static class PackDefinitions
{
    public const string CoreSetId = "CoreSet";
    public const string QuietId = "Quiet";
    public const string BitingColdId = "BitingCold";

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

        PackRegistry.Register(new PackDefinition(
            Id: QuietId,
            AlwaysInclude: false,
            CardTypes:
            [
                typeof(Oppressor),
                typeof(IronShield),
                typeof(Shine),
                typeof(GlaiveThrow),
                typeof(Crunch),
                typeof(Lie),
                typeof(Frontflip),
                typeof(HammerThrow),
                typeof(Flail),
                typeof(Wisdom),
            ]));

        PackRegistry.Register(new PackDefinition(
            Id: BitingColdId,
            AlwaysInclude: false,
            CardTypes:
            [
                typeof(FrigidBody),
                typeof(Frostburn),
                typeof(FrostedEdge),
                typeof(GrowingAffliction),
                typeof(IceShatter),
                typeof(InsultToInjury),
                typeof(Refrigerate),
                typeof(RelentlessHail),
                typeof(SnowedIn),
                typeof(Subdue),
            ]));
    }
}
