using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Automaton.Cards;

/// <summary>
/// Thunder Wave (Rare, 2 cost, Exhaust):
/// Deal 12 (16) damage to ALL enemies. Gain 1 Buffer.
/// </summary>
public sealed class ThunderWave() : BronzeCard(2, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(12m, ValueProp.Move),
        new PowerVar<BufferPower>(1m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<BufferPower>()
    ];

    public override async Task ExecuteEffectAsync(
        PlayerChoiceContext choiceContext,
        Player owner,
        Creature? target,
        CardPlay? cardPlay,
        CardModel? sourceCard,
        bool isFromPower)
    {
        if (!owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        await DealAllEnemiesDamageAsync(choiceContext, owner, cardPlay, sourceCard, isFromPower, DynamicVars.Damage.BaseValue);

        if (owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Apply<BufferPower>(
                choiceContext,
                owner.Creature,
                DynamicVars[nameof(BufferPower)].BaseValue,
                owner.Creature,
                isFromPower ? null : sourceCard);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
    }
}
