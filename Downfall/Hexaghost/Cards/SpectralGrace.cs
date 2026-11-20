using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Hexaghost.Cards;

/// <summary>
/// Discard 2 cards. Deal 13 (17) damage.
/// </summary>
public sealed class SpectralGrace() : HexaCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(13m, ValueProp.Move),
        new CardsVar(2)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        var eligible = PileType.Hand.GetPile(Owner).Cards
            .Where(c => !ReferenceEquals(c, this))
            .ToList();
        if (eligible.Count > 0)
        {
            int discardCount = Math.Min(DynamicVars.Cards.IntValue, eligible.Count);
            List<CardModel> toDiscard;
            if (eligible.Count <= discardCount)
            {
                toDiscard = eligible;
            }
            else
            {
                var prefs = new CardSelectorPrefs(SelectionScreenPrompt, discardCount);
                toDiscard = (await CardSelectCmd.FromHandForDiscard(choiceContext, Owner, prefs, c => !ReferenceEquals(c, this), this)).ToList();
            }

            if (toDiscard.Count > 0 && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
                await CardCmd.Discard(choiceContext, toDiscard);
        }

        if (cardPlay.Target.IsAlive && Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
    }
}
