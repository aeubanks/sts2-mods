using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Hexaghost.Cards;

/// <summary>
/// Ethereal. Afterlife. Deal 10 (13) damage. Exhaust 1 card. Draw 1 (2) card(s).
/// Afterlife: Deal 10 (13) damage to a random enemy.
/// </summary>
public sealed class EtherStep() : HexaCard(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal];
    public override bool HasAfterlife => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(10m, ValueProp.Move),
        new CardsVar(1)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        AfterlifeHoverTip,
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var eligible = PileType.Hand.GetPile(Owner).Cards
            .Where(c => !ReferenceEquals(c, this))
            .ToList();
        if (eligible.Count > 0)
        {
            CardModel? chosen;
            if (eligible.Count == 1)
            {
                chosen = eligible[0];
            }
            else
            {
                var prefs = new CardSelectorPrefs(SelectionScreenPrompt, 1);
                chosen = (await CardSelectCmd.FromHand(choiceContext, Owner, prefs, c => !ReferenceEquals(c, this), this)).FirstOrDefault();
            }

            if (chosen is not null && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
                await CardCmd.Exhaust(choiceContext, chosen, causedByEthereal: false);
        }

        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
    }

    protected override async Task OnAfterlifeAsync(PlayerChoiceContext choiceContext, bool causedByEthereal)
    {
        var combatState = Owner.Creature.CombatState;
        if (combatState is null)
            return;

        var enemies = combatState.HittableEnemies.Where(e => e.IsAlive).ToList();
        var target = Owner.RunState.Rng.CombatTargets.NextItem(enemies);
        if (target is null)
            return;

        await CreatureCmd.Damage(
            choiceContext,
            target,
            DynamicVars.Damage.BaseValue,
            ValueProp.Move,
            Owner.Creature,
            this,
            null);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
