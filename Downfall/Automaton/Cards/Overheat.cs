using Downfall.Automaton.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
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
/// Overheat (Common, 2 cost):
/// Deal 18 (22) damage. Remove the next Compile Error you would Encode.
/// </summary>
public sealed class Overheat() : BronzeCard(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(18m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        CompileHoverTip,
        EncodeHoverTip,
        HoverTipFactory.FromPower<RemoveNextErrorPower>()
    ];

    public override async Task ExecuteEffectAsync(
        PlayerChoiceContext choiceContext,
        Player owner,
        Creature? target,
        CardPlay? cardPlay,
        CardModel? sourceCard,
        bool isFromPower)
    {
        await DealDamageAsync(choiceContext, owner, target, cardPlay, sourceCard, isFromPower, DynamicVars.Damage.BaseValue);
        if (owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Apply<RemoveNextErrorPower>(
                choiceContext,
                owner.Creature,
                1m,
                owner.Creature,
                isFromPower ? null : sourceCard);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
    }
}
