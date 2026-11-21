using Downfall.Core;
using Downfall.Guardian.Cards;
using Downfall.Guardian.Relics;
using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Guardian;

public sealed class TheGuardian : DownfallCharacter
{
    public const string CharacterId = "TheGuardian";
    public const string CharKey = "guardian";

    public static readonly Color CharacterColor = new("#C98C3E");

    public override string CharacterKey => CharKey;
    public override Color NameColor => new("#E0A952");
    public override Color EnergyLabelOutlineColor => new("#4D3212");
    public override CharacterGender Gender => CharacterGender.Neutral;
    public override int StartingHp => 80;
    public override int StartingGold => 99;
    public override int BaseOrbSlotCount => 3;

    public override CardPoolModel CardPool => ModelDb.CardPool<GuardianCardPool>();
    public override RelicPoolModel RelicPool => ModelDb.RelicPool<GuardianRelicPool>();
    public override PotionPoolModel PotionPool => ModelDb.PotionPool<GuardianPotionPool>();

    public override IEnumerable<CardModel> StartingDeck =>
    [
        ModelDb.Card<GuardianStrike>(),
        ModelDb.Card<GuardianStrike>(),
        ModelDb.Card<GuardianStrike>(),
        ModelDb.Card<GuardianStrike>(),
        ModelDb.Card<GuardianDefend>(),
        ModelDb.Card<GuardianDefend>(),
        ModelDb.Card<GuardianDefend>(),
        ModelDb.Card<GuardianDefend>(),
        ModelDb.Card<CurlUp>(),
        ModelDb.Card<TwinSlam>()
    ];

    public override IReadOnlyList<RelicModel> StartingRelics =>
    [
        ModelDb.Relic<ModeShifter>()
    ];
}
