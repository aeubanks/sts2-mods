using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Hermit.Cards;

public sealed class Shortfuse()
    : HermitCard(3, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override string PortraitKey => "shortfuse";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(18m, ValueProp.Move)
    ];

    private static bool IsStrikeOrDefend(CardModel card) =>
        card.Tags.Contains(CardTag.Strike) || card.Tags.Contains(CardTag.Defend);

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);
    }

    public override Task AfterCardEnteredCombat(CardModel card)
    {
        if (!ReferenceEquals(card, this) || IsClone || CombatState is null)
            return Task.CompletedTask;

        int count = CombatManager.Instance.History.CardPlaysFinished.Count(e =>
            e.CardPlay.Player == Owner
            && e.HappenedThisTurn(CombatState)
            && IsStrikeOrDefend(e.CardPlay.Card));
        if (count > 0)
        {
            EnergyCost.AddThisTurn(-count);
        }
        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner == Owner && IsStrikeOrDefend(cardPlay.Card))
        {
            EnergyCost.AddThisTurn(-1);
        }
        return base.AfterCardPlayed(choiceContext, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
    }
}
