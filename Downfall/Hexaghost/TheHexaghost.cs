using Downfall.Core;
using Downfall.Hexaghost.Cards;
using Downfall.Hexaghost.Relics;
using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Hexaghost;

public sealed class TheHexaghost : DownfallCharacter
{
    public const string CharacterId = "TheHexaghost";
    public const string CharKey = "hexaghost";

    public static readonly Color CharacterColor = new("#723E6D");

    public override string CharacterKey => CharKey;
    public override DownfallIdleStyle IdleStyle => DownfallIdleStyle.FloatingMystical;
    public override Color NameColor => new("#7FE898");
    public override Color EnergyLabelOutlineColor => new("#2E1430");
    public override CharacterGender Gender => CharacterGender.Neutral;
    public override int StartingHp => 66;
    public override int StartingGold => 99;
    public override int BaseOrbSlotCount => 0;

    public override CardPoolModel CardPool => ModelDb.CardPool<HexaCardPool>();
    public override RelicPoolModel RelicPool => ModelDb.RelicPool<HexaRelicPool>();
    public override PotionPoolModel PotionPool => ModelDb.PotionPool<HexaPotionPool>();

    public override IEnumerable<CardModel> StartingDeck =>
    [
        ModelDb.Card<HexaStrike>(),
        ModelDb.Card<HexaStrike>(),
        ModelDb.Card<HexaStrike>(),
        ModelDb.Card<HexaStrike>(),
        ModelDb.Card<HexaDefend>(),
        ModelDb.Card<HexaDefend>(),
        ModelDb.Card<HexaDefend>(),
        ModelDb.Card<HexaDefend>(),
        ModelDb.Card<Sear>(),
        ModelDb.Card<Float>(),
        ModelDb.Card<SkipABeat>()
    ];

    public override IReadOnlyList<RelicModel> StartingRelics =>
    [
        ModelDb.Relic<SpiritBrand>()
    ];
}
