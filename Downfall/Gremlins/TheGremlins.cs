using Downfall.Core;
using Downfall.Gremlins.Cards;
using Downfall.Gremlins.Relics;
using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Gremlins;

public sealed class TheGremlins : DownfallCharacter
{
    public const string CharacterId = "TheGremlins";
    public const string CharKey = "gremlins";

    public static readonly Color CharacterColor = new(0.86f, 0.25f, 0.22f);

    public override string CharacterKey => CharKey;
    public override Color NameColor => CharacterColor;
    public override CharacterGender Gender => CharacterGender.Neutral;
    public override int StartingHp => 16;

    public override IEnumerable<CardModel> StartingDeck =>
    [
        ModelDb.Card<GremlinStrike>(),
        ModelDb.Card<GremlinStrike>(),
        ModelDb.Card<GremlinStrike>(),
        ModelDb.Card<GremlinStrike>(),
        ModelDb.Card<GremlinDefend>(),
        ModelDb.Card<GremlinDefend>(),
        ModelDb.Card<GremlinDefend>(),
        ModelDb.Card<GremlinDefend>(),
        ModelDb.Card<TagTeam>(),
        ModelDb.Card<GremlinDance>()
    ];

    public override IReadOnlyList<RelicModel> StartingRelics =>
    [
        ModelDb.Relic<GremlinKnob>()
    ];

    public override CardPoolModel CardPool => ModelDb.CardPool<GremlinCardPool>();
    public override RelicPoolModel RelicPool => ModelDb.RelicPool<GremlinRelicPool>();
    public override PotionPoolModel PotionPool => ModelDb.PotionPool<GremlinPotionPool>();
}
