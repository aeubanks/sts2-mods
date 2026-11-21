using Downfall.Core;
using Downfall.Snecko.Cards;
using Downfall.Snecko.Relics;
using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Snecko;

public sealed class TheSnecko : DownfallCharacter
{
    public const string CharacterId = "TheSnecko";
    public const string CharKey = "snecko";

    public static readonly Color CharacterColor = new(0.25f, 0.48f, 0.58f);

    public override string CharacterKey => CharKey;
    public override Color NameColor => CharacterColor;
    public override CharacterGender Gender => CharacterGender.Neutral;
    public override int StartingHp => 85;

    public override IEnumerable<CardModel> StartingDeck =>
    [
        ModelDb.Card<SneckoStrike>(),
        ModelDb.Card<SneckoStrike>(),
        ModelDb.Card<SneckoStrike>(),
        ModelDb.Card<SneckoStrike>(),
        ModelDb.Card<SneckoDefend>(),
        ModelDb.Card<SneckoDefend>(),
        ModelDb.Card<SneckoDefend>(),
        ModelDb.Card<SneckoDefend>(),
        ModelDb.Card<TailWhip>(),
        ModelDb.Card<SnekBite>()
    ];

    public override IReadOnlyList<RelicModel> StartingRelics =>
    [
        ModelDb.Relic<SneckoSoul>()
    ];

    public override CardPoolModel CardPool => ModelDb.CardPool<SneckoCardPool>();
    public override RelicPoolModel RelicPool => ModelDb.RelicPool<SneckoRelicPool>();
    public override PotionPoolModel PotionPool => ModelDb.PotionPool<SneckoPotionPool>();
}
