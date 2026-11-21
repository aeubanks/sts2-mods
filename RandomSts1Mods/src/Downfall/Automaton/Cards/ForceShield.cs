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
/// Force Shield (Uncommon, 4 cost):
/// Costs 1 less Energy for each Function created this combat. Gain 12 (16) Block.
/// </summary>
public sealed class ForceShield() : BronzeCard(4, CardType.Skill, CardRarity.Uncommon, TargetType.Self), IFunctionCompiledCardListener
{
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(12m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        FunctionHoverTip
    ];

    public override Task AfterCardEnteredCombat(CardModel card)
    {
        if (!ReferenceEquals(card, this) || IsClone || CombatState is null || Owner is null)
            return Task.CompletedTask;

        int compiled = AutomatonMechanics.GetFunctionsCompiledThisCombat(Owner);
        if (compiled > 0)
            EnergyCost.AddThisCombat(-compiled);
        return Task.CompletedTask;
    }

    public void OnFunctionCompiled(Player player, int totalFunctionsCompiled)
    {
        if (player == Owner)
            EnergyCost.AddThisCombat(-1);
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
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(4m);
    }
}
