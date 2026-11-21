using Downfall.Champ.Cards;
using Downfall.Champ.Relics;
using Downfall.Core;
using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Champ;

public sealed class TheChamp : DownfallCharacter
{
    public const string CharacterId = "TheChamp";
    public const string CharKey = "champ";

    public static readonly Color CharacterColor = new(0.84f, 0.72f, 0.28f);

    public override string CharacterKey => CharKey;
    public override Color NameColor => CharacterColor;
    public override CharacterGender Gender => CharacterGender.Masculine;
    public override int StartingHp => 80;

    public override IEnumerable<CardModel> StartingDeck =>
    [
        ModelDb.Card<ChampStrike>(),
        ModelDb.Card<ChampStrike>(),
        ModelDb.Card<ChampStrike>(),
        ModelDb.Card<ChampStrike>(),
        ModelDb.Card<ChampDefend>(),
        ModelDb.Card<ChampDefend>(),
        ModelDb.Card<ChampDefend>(),
        ModelDb.Card<BerserkersShout>(),
        ModelDb.Card<DefensiveShout>(),
        ModelDb.Card<Execute>()
    ];

    public override IReadOnlyList<RelicModel> StartingRelics =>
    [
        ModelDb.Relic<ChampionCrown>()
    ];

    public override CardPoolModel CardPool => ModelDb.CardPool<ChampCardPool>();
    public override RelicPoolModel RelicPool => ModelDb.RelicPool<ChampRelicPool>();
    public override PotionPoolModel PotionPool => ModelDb.PotionPool<ChampPotionPool>();
}
