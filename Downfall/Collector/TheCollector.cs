using Downfall.Collector.Cards;
using Downfall.Collector.Relics;
using Downfall.Core;
using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Collector;

public sealed class TheCollector : DownfallCharacter
{
    public const string CharacterId = "TheCollector";
    public const string CharKey = "collector";

    public static readonly Color CharacterColor = new("#0D9E83");

    public override string CharacterKey => CharKey;
    public override Color NameColor => new("#3AD6B5");
    public override Color EnergyLabelOutlineColor => new("#0B3B31");
    public override CharacterGender Gender => CharacterGender.Feminine;
    public override int StartingHp => 65;
    public override int StartingGold => 99;

    public override CardPoolModel CardPool => ModelDb.CardPool<CollectorCardPool>();
    public override RelicPoolModel RelicPool => ModelDb.RelicPool<CollectorRelicPool>();
    public override PotionPoolModel PotionPool => ModelDb.PotionPool<CollectorPotionPool>();

    public override IEnumerable<CardModel> StartingDeck =>
    [
        ModelDb.Card<CollectorStrike>(),
        ModelDb.Card<CollectorStrike>(),
        ModelDb.Card<CollectorStrike>(),
        ModelDb.Card<CollectorStrike>(),
        ModelDb.Card<CollectorDefend>(),
        ModelDb.Card<CollectorDefend>(),
        ModelDb.Card<CollectorDefend>(),
        ModelDb.Card<CollectorDefend>(),
        ModelDb.Card<YouAreMine>(),
        ModelDb.Card<FuelTheFire>()
    ];

    public override IReadOnlyList<RelicModel> StartingRelics =>
    [
        ModelDb.Relic<EmeraldTorch>()
    ];
}
