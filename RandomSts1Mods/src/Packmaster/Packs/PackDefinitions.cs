using Packmaster.Cards.Batter;
using Packmaster.Cards.BitingCold;
using Packmaster.Cards.CoreSet;
using Packmaster.Cards.Distortion;
using Packmaster.Cards.EnergyAndEcho;
using Packmaster.Cards.EvenOdd;
using Packmaster.Cards.Farmer;
using Packmaster.Cards.Hermit;
using Packmaster.Cards.HighEnergy;
using Packmaster.Cards.Quiet;
using Packmaster.Cards.Shaman;
using Packmaster.Cards.Warden;
using Packmaster.Cards.Warlock;

namespace Packmaster.Packs;

public static class PackDefinitions
{
    public const string CoreSetId = "CoreSet";
    public const string QuietId = "Quiet";
    public const string BitingColdId = "BitingCold";
    public const string WarlockId = "Warlock";
    public const string EvenOddId = "EvenOdd";
    public const string HighEnergyId = "HighEnergy";
    public const string BatterId = "Batter";
    public const string EnergyAndEchoId = "EnergyAndEcho";
    public const string ShamanId = "Shaman";
    public const string WardenId = "Warden";
    public const string FarmerId = "Farmer";
    public const string HermitId = "Hermit";
    public const string DistortionId = "Distortion";

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

        PackRegistry.Register(new PackDefinition(
            Id: HighEnergyId,
            AlwaysInclude: false,
            CardTypes:
            [
                typeof(Investor),
                typeof(LastResort),
                typeof(LotsOfIronWaves),
                typeof(ManifestMeal),
                typeof(MiracleWorker),
                typeof(MultiTool),
                typeof(StormForm),
                typeof(StruckByATrain),
                typeof(SuperNova),
                typeof(Trash),
            ]));

        PackRegistry.Register(new PackDefinition(
            Id: BatterId,
            AlwaysInclude: false,
            CardTypes:
            [
                typeof(AvatarBeat),
                typeof(BatterUp),
                typeof(HardCaramel),
                typeof(Homerun),
                typeof(OpenBracket),
                typeof(OptimizedBlur),
                typeof(Tragedy),
                typeof(UltimateHomerun),
                typeof(WideAngle),
                typeof(WindUp),
            ]));

        PackRegistry.Register(new PackDefinition(
            Id: EnergyAndEchoId,
            AlwaysInclude: false,
            CardTypes:
            [
                typeof(Crystallize),
                typeof(Discharge),
                typeof(Flashlight),
                typeof(MagnetRise),
                typeof(Overload),
                typeof(Panacher),
                typeof(ShadowStrike),
                typeof(Slowing),
                typeof(TearApart),
                typeof(Voices),
            ]));

        PackRegistry.Register(new PackDefinition(
            Id: ShamanId,
            AlwaysInclude: false,
            CardTypes:
            [
                typeof(FieryMantle),
                typeof(FlameBlast),
                typeof(Flare),
                typeof(FreezeAndBurn),
                typeof(Frostfire),
                typeof(FueledByEmbers),
                typeof(IceWall),
                typeof(Pyromastery),
                typeof(ReadTheFlames),
                typeof(SnowMelt),
            ]));

        PackRegistry.Register(new PackDefinition(
            Id: WardenId,
            AlwaysInclude: false,
            CardTypes:
            [
                typeof(Anticipation),
                typeof(Crossroads),
                typeof(DivineMight),
                typeof(Enigmancy),
                typeof(ForceBolt),
                typeof(PierceVeil),
                typeof(Prediction),
                typeof(Recurrence),
                typeof(RiskOfBlades),
                typeof(Stall),
            ]));

        PackRegistry.Register(new PackDefinition(
            Id: FarmerId,
            AlwaysInclude: false,
            CardTypes:
            [
                typeof(HarvestBeans),
                typeof(HarvestCarrots),
                typeof(HarvestPotatoes),
                typeof(HarvestRadishes),
                typeof(OutToMarket),
                typeof(PestControl),
                typeof(SoilResiliency),
                typeof(SowTheSeeds),
                typeof(SupplyChain),
                typeof(TillTheEarth),
            ]));

        PackRegistry.Register(new PackDefinition(
            Id: HermitId,
            AlwaysInclude: false,
            CardTypes:
            [
                typeof(DeadOrAlive),
                typeof(Determination),
                typeof(FlashPowder),
                typeof(Glare),
                typeof(Headshot),
                typeof(HoleUp),
                typeof(OverwhelmingPower),
                typeof(Purgatory),
                typeof(Quickdraw),
                typeof(Virtue),
            ]));

        PackRegistry.Register(new PackDefinition(
            Id: DistortionId,
            AlwaysInclude: false,
            CardTypes:
            [
                typeof(BurningAct),
                typeof(CursedCircle),
                typeof(Darken),
                typeof(Deconstruct),
                typeof(Deterioration),
                typeof(MindMaze),
                typeof(Rue),
                typeof(Shatter),
                typeof(Srke),
                typeof(Static),
            ]));
    }
}
