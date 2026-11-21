using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Automaton.Cards;

/// <summary>
/// Delayed Guard (Common, 0 cost, Encodes):
/// Next turn, gain 7 (10) Block.
/// </summary>
public sealed class DelayedGuard() : BronzeCard(0, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    public override bool Encodes => true;
    public override string FunctionAdjective => "delayed";
    public override string FunctionNoun => "Guard";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(7m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        EncodeHoverTip,
        FunctionHoverTip,
        HoverTipFactory.Static(StaticHoverTip.Block)
    ];

    public override string GetFunctionEffectSummary(Player? owner, Creature? target) =>
        $"Next turn, gain [blue]{DynamicVars.Block.IntValue}[/blue] [gold]Block[/gold].";

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

        decimal blockAmount = isFromPower
            ? DynamicVars.Block.BaseValue
            : Hook.ModifyBlock(combatState, owner.Creature, DynamicVars.Block.BaseValue, ValueProp.Move, sourceCard, cardPlay, out _);

        decimal floored = Math.Floor(blockAmount);
        if (floored > 0m)
        {
            await PowerCmd.Apply<BlockNextTurnPower>(
                choiceContext,
                owner.Creature,
                floored,
                owner.Creature,
                isFromPower ? null : sourceCard);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}
