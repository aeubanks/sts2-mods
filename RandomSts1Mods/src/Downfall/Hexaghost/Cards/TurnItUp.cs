using Downfall.Hexaghost.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Hexaghost.Cards;

/// <summary>
/// Gain 3 temporary Intensity, 3 temporary Strength, and 3 temporary Dexterity.
/// Upgraded: adds Retain.
/// </summary>
public sealed class TurnItUp() : HexaCard(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Amount", 3m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        IntensityHoverTip,
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<DexterityPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        decimal amt = DynamicVars["Amount"].BaseValue;
        await PowerCmd.Apply<EnhancePower>(choiceContext, Owner.Creature, amt, Owner.Creature, this);
        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;
        await PowerCmd.Apply<LoseIntensityPower>(choiceContext, Owner.Creature, amt, Owner.Creature, this);
        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;
        await PowerCmd.Apply<FlexPotionPower>(choiceContext, Owner.Creature, amt, Owner.Creature, this);
        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;
        await PowerCmd.Apply<SpeedPotionPower>(choiceContext, Owner.Creature, amt, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}
