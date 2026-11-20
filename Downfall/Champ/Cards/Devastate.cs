using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Champ.Cards;

public sealed class Devastate()
    : ChampCard(5, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6m, ValueProp.Move),
        new RepeatVar(3)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        FinisherHoverTip
    ];

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        if (ReferenceEquals(card, this) && CombatState is not null)
        {
            int finishersPlayed = CombatManager.Instance.History.CardPlaysFinished.Count(e =>
                e.CardPlay.Card.Owner == Owner
                && e.CardPlay.Card is ChampCard { IsFinisher: true });

            if (finishersPlayed > 0)
            {
                modifiedCost = Math.Max(0m, originalCost - finishersPlayed);
                return true;
            }
        }

        modifiedCost = originalCost;
        return false;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(DynamicVars.Repeat.IntValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash", null, "heavy_attack.mp3")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
    }
}
