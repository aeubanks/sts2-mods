using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Downfall.Automaton.Cards;

/// <summary>
/// Undervolt (Uncommon, 1 cost, Exhaust):
/// ALL enemies lose 2 (3) Strength. Add 2 Burns to your Hand.
/// </summary>
public sealed class Undervolt() : BronzeCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<StrengthPower>(2m),
        new IntVar("Burns", 2m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromCard<Burn>()
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

        decimal strLoss = DynamicVars[nameof(StrengthPower)].BaseValue;
        foreach (var enemy in combatState.HittableEnemies.Where(e => e.IsAlive).ToList())
        {
            if (CombatManager.Instance.IsOverOrEnding)
                return;
            await PowerCmd.Apply<StrengthPower>(
                choiceContext,
                enemy,
                -strLoss,
                owner.Creature,
                isFromPower ? null : sourceCard);
        }

        int burnCount = DynamicVars["Burns"].IntValue;
        for (int i = 0; i < burnCount; i++)
        {
            if (!owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;
            var burn = combatState.CreateCard<Burn>(owner);
            await AutomatonMechanics.AddGeneratedCardToHandOrDiscardAsync(burn, owner);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars[nameof(StrengthPower)].UpgradeValueBy(1m);
    }
}
