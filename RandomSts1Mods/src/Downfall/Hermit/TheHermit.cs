using Downfall.Core;
using Downfall.Hermit.Cards;
using Downfall.Hermit.Relics;
using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Hermit;

public sealed class TheHermit : DownfallCharacter
{
    public const string CharacterId = "TheHermit";
    public const string CharKey = "hermit";

    public static readonly Color CharacterColor = new(0.85f, 0.72f, 0.25f);

    public override string CharacterKey => CharKey;
    public override Color NameColor => CharacterColor;
    public override CharacterGender Gender => CharacterGender.Neutral;
    public override int StartingHp => 75;

    public override IEnumerable<CardModel> StartingDeck =>
    [
        ModelDb.Card<HermitStrike>(),
        ModelDb.Card<HermitStrike>(),
        ModelDb.Card<HermitStrike>(),
        ModelDb.Card<HermitStrike>(),
        ModelDb.Card<HermitDefend>(),
        ModelDb.Card<HermitDefend>(),
        ModelDb.Card<HermitDefend>(),
        ModelDb.Card<HermitDefend>(),
        ModelDb.Card<Snapshot>(),
        ModelDb.Card<Covet>()
    ];

    public override IReadOnlyList<RelicModel> StartingRelics =>
    [
        ModelDb.Relic<OldLocket>()
    ];

    public override CardPoolModel CardPool => ModelDb.CardPool<HermitCardPool>();
    public override RelicPoolModel RelicPool => ModelDb.RelicPool<HermitRelicPool>();
    public override PotionPoolModel PotionPool => ModelDb.PotionPool<HermitPotionPool>();
}
