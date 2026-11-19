using Packmaster.Cards.BitingCold;
using Packmaster.Cards.CoreSet;
using Packmaster.Cards.EvenOdd;
using Packmaster.Cards.Quiet;
using Packmaster.Cards.Warlock;

namespace Packmaster.Packs;

public static class PackDefinitions
{
    public const string CoreSetId = "CoreSet";
    public const string QuietId = "Quiet";
    public const string BitingColdId = "BitingCold";
    public const string WarlockId = "Warlock";
    public const string EvenOddId = "EvenOdd";

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

        PackRegistry.Register(new PackDefinition(
            Id: WarlockId,
            AlwaysInclude: false,
            CardTypes:
            [
                typeof(AranasiBroodmother),
                typeof(Doomguard),
                typeof(HandOfGulDan),
                typeof(HighPriestessJeklik),
                typeof(MalchezaarsImp),
                typeof(RunedMithrilRod),
                typeof(SeedsOfDestruction),
                typeof(SoulShear),
                typeof(Soulfire),
                typeof(TheSoularium),
            ]));

        PackRegistry.Register(new PackDefinition(
            Id: EvenOddId,
            AlwaysInclude: false,
            CardTypes:
            [
                typeof(Battlecry),
                typeof(ElementOfSurprise),
                typeof(Flourish),
                typeof(GammaWard),
                typeof(LeftHook),
                typeof(PrimeDirective),
                typeof(QuickReflex),
                typeof(SwordAndBoard),
                typeof(WindUpBomb),
                typeof(WitzBolt),
            ]));
    }
}
