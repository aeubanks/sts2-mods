using Downfall.Awakened.Cards;
using Downfall.Awakened.Relics;
using Downfall.Core;
using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Awakened;

public sealed class TheAwakenedOne : DownfallCharacter
{
    public const string CharacterId = "TheAwakenedOne";
    public const string CharKey = "awakened";

    public static readonly Color CharacterColor = new(0.24f, 0.72f, 0.90f);

    public override string CharacterKey => CharKey;
    public override Color NameColor => CharacterColor;
    public override CharacterGender Gender => CharacterGender.Neutral;
    public override int StartingHp => 70;

    public override IEnumerable<CardModel> StartingDeck =>
    [
        ModelDb.Card<AwakenedStrike>(),
        ModelDb.Card<AwakenedStrike>(),
        ModelDb.Card<AwakenedStrike>(),
        ModelDb.Card<AwakenedStrike>(),
        ModelDb.Card<AwakenedDefend>(),
        ModelDb.Card<AwakenedDefend>(),
        ModelDb.Card<AwakenedDefend>(),
        ModelDb.Card<AwakenedDefend>(),
        ModelDb.Card<TalonRake>(),
        ModelDb.Card<Hymn>()
    ];

    public override IReadOnlyList<RelicModel> StartingRelics =>
    [
        ModelDb.Relic<RippedDoll>()
    ];

    public override CardPoolModel CardPool => ModelDb.CardPool<AwakenedCardPool>();
    public override RelicPoolModel RelicPool => ModelDb.RelicPool<AwakenedRelicPool>();
    public override PotionPoolModel PotionPool => ModelDb.PotionPool<AwakenedPotionPool>();
}
