using Downfall.Automaton.Cards;
using Downfall.Automaton.Relics;
using Downfall.Core;
using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Automaton;

public sealed class TheAutomaton : DownfallCharacter
{
    public const string CharacterId = "TheAutomaton";
    public const string CharKey = "automaton";

    public static readonly Color CharacterColor = new("#D6CA9E");

    public override string CharacterKey => CharKey;
    public override DownfallIdleStyle IdleStyle => DownfallIdleStyle.Floating;
    public override Color NameColor => new("#E6DCAA");
    public override Color EnergyLabelOutlineColor => new("#3D3219");
    public override CharacterGender Gender => CharacterGender.Neutral;
    public override int StartingHp => 70;
    public override int StartingGold => 99;
    public override int BaseOrbSlotCount => 0;

    public override CardPoolModel CardPool => ModelDb.CardPool<AutomatonCardPool>();
    public override RelicPoolModel RelicPool => ModelDb.RelicPool<AutomatonRelicPool>();
    public override PotionPoolModel PotionPool => ModelDb.PotionPool<AutomatonPotionPool>();

    public override IEnumerable<CardModel> StartingDeck =>
    [
        ModelDb.Card<BronzeStrike>(),
        ModelDb.Card<BronzeStrike>(),
        ModelDb.Card<BronzeStrike>(),
        ModelDb.Card<BronzeStrike>(),
        ModelDb.Card<BronzeDefend>(),
        ModelDb.Card<BronzeDefend>(),
        ModelDb.Card<BronzeDefend>(),
        ModelDb.Card<BronzeDefend>(),
        ModelDb.Card<Goto>(),
        ModelDb.Card<Replicate>()
    ];

    public override IReadOnlyList<RelicModel> StartingRelics =>
    [
        ModelDb.Relic<BronzeCore>()
    ];
}
