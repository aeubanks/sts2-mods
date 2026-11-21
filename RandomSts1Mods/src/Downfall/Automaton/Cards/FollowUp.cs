using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Automaton.Cards;

/// <summary>
/// Follow Through (FollowUp, Uncommon, 1 cost):
/// Gain 4 (6) Block. Deal 7 (9) damage. If the last played card was a Function, play this again.
/// </summary>
public sealed class FollowUp() : BronzeCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(4m, ValueProp.Move),
        new DamageVar(7m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        FunctionHoverTip
    ];

    private bool WasLastCardAFunction()
    {
        if (!IsMutable || Owner is null || CombatState is null)
            return false;

        var last = CombatManager.Instance.History.CardPlaysFinished
            .LastOrDefault(e => e.CardPlay.Player == Owner);
        return last?.CardPlay.Card is FunctionCard;
    }

    protected override bool ShouldGlowGoldInternal => WasLastCardAFunction();

    public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
    {
        if (card == this && WasLastCardAFunction())
            return playCount + 1;
        return playCount;
    }

    public override async Task ExecuteEffectAsync(
        PlayerChoiceContext choiceContext,
        Player owner,
        Creature? target,
        CardPlay? cardPlay,
        CardModel? sourceCard,
        bool isFromPower)
    {
        await GainBlockAsync(owner, cardPlay, isFromPower, DynamicVars.Block.BaseValue);
        if (owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            await DealDamageAsync(choiceContext, owner, target, cardPlay, sourceCard, isFromPower, DynamicVars.Damage.BaseValue);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
