using Downfall.Core;
using Downfall.Slimebound.Cards;
using Downfall.Slimebound.Relics;
using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Slimebound;

public sealed class TheSlimeBoss : DownfallCharacter
{
    public const string CharacterId = "TheSlimeBoss";
    public const string CharKey = "slimebound";

    public static readonly Color CharacterColor = new("#3DB36B");

    public override string CharacterKey => CharKey;
    public override Color NameColor => new("#6FE36B");
    public override Color EnergyLabelOutlineColor => new("#1D5223");
    public override CharacterGender Gender => CharacterGender.Neutral;
    public override int StartingHp => 65;
    public override int StartingGold => 99;
    public override int BaseOrbSlotCount => 3;

    public override CardPoolModel CardPool => ModelDb.CardPool<SlimeboundCardPool>();
    public override RelicPoolModel RelicPool => ModelDb.RelicPool<SlimeboundRelicPool>();
    public override PotionPoolModel PotionPool => ModelDb.PotionPool<SlimeboundPotionPool>();

    public override IEnumerable<CardModel> StartingDeck =>
    [
        ModelDb.Card<SlimeboundStrike>(),
        ModelDb.Card<SlimeboundStrike>(),
        ModelDb.Card<SlimeboundStrike>(),
        ModelDb.Card<Tackle>(),
        ModelDb.Card<SlimeboundDefend>(),
        ModelDb.Card<SlimeboundDefend>(),
        ModelDb.Card<SlimeboundDefend>(),
        ModelDb.Card<SlimeboundDefend>(),
        ModelDb.Card<Split>(),
        ModelDb.Card<CorrosiveSpit>()
    ];

    public override IReadOnlyList<RelicModel> StartingRelics =>
    [
        ModelDb.Relic<AbsorbEndCombat>()
    ];
}
