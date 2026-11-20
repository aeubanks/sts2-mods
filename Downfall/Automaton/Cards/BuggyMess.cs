using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;

namespace Downfall.Automaton.Cards;

/// <summary>
/// Buggy Mess (Common, 1 -> 0 cost, Encodes):
/// Insert a Dazed. Gain 1 Energy.
/// </summary>
public sealed class BuggyMess() : BronzeCard(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    public override bool Encodes => true;
    public override string FunctionAdjective => "buggy";
    public override string FunctionNoun => "Mess";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(1)
    ];

    protected override IEnumerable<string> ExtraFineTunableVarNames => ["Energy"];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        EncodeHoverTip,
        InsertHoverTip,
        FunctionHoverTip,
        HoverTipFactory.FromCard<Dazed>()
    ];

    public override string GetFunctionEffectSummary(Player? owner, Creature? target) =>
        $"[gold]Insert[/gold] a [gold]Dazed[/gold]. Gain [blue]{DynamicVars.Energy.IntValue}[/blue] Energy.";

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

        await AutomatonMechanics.InsertCardIntoDrawPileAsync<Dazed>(owner, 1);
        if (owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, owner);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
