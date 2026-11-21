using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Automaton.Cards;

/// <summary>
/// Format (Rare, X cost, Exhaust):
/// Encode X (+1 when upgraded) copies of Fragment. Gain 1 Energy.
/// </summary>
public sealed class Format() : BronzeCard(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    protected override bool HasEnergyCostX => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Bonus", 0m),
        new EnergyVar(1)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        EncodeHoverTip,
        FunctionHoverTip,
        HoverTipFactory.FromCard<FormatEncoded>()
    ];

    public override async Task ExecuteEffectAsync(
        PlayerChoiceContext choiceContext,
        Player owner,
        Creature? target,
        CardPlay? cardPlay,
        CardModel? sourceCard,
        bool isFromPower)
    {
        if (owner.Creature.CombatState is not { } combatState
            || !owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        int count = ResolveEnergyXValue() + DynamicVars["Bonus"].IntValue;
        for (int i = 0; i < count; i++)
        {
            if (!owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                return;

            var fragment = (BronzeCard)combatState.CreateCard<FormatEncoded>(owner);
            await AutomatonMechanics.EncodeCardAsync(choiceContext, owner, fragment);
        }

        if (owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Bonus"].UpgradeValueBy(1m);
    }
}
