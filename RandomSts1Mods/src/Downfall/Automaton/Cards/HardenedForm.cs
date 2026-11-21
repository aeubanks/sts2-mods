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

namespace Downfall.Automaton.Cards;

/// <summary>
/// Summon Orb (HardenedForm, Rare, 2 cost, Power):
/// Whenever you create or play a Function, gain 3 (4) Block and deal 3 (4) damage to a random enemy.
/// </summary>
public sealed class HardenedForm() : BronzeCard(2, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<HardenedFormPower>(3m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        FunctionHoverTip,
        HoverTipFactory.FromPower<HardenedFormPower>(),
        HoverTipFactory.Static(StaticHoverTip.Block)
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

        await PowerCmd.Apply<HardenedFormPower>(
            choiceContext,
            owner.Creature,
            DynamicVars[nameof(HardenedFormPower)].BaseValue,
            owner.Creature,
            isFromPower ? null : sourceCard);
    }

    protected override void OnUpgrade()
    {
        DynamicVars[nameof(HardenedFormPower)].UpgradeValueBy(1m);
    }
}
