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
/// Safeguard (Common, 0 cost, Encodes): Gain 4 (6) Block.
/// </summary>
public sealed class Safeguard() : BronzeCard(0, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    public override bool Encodes => true;
    public override bool GainsBlock => true;
    public override string FunctionAdjective => "safe";
    public override string FunctionNoun => "Guard";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(4m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        EncodeHoverTip,
        FunctionHoverTip
    ];

    public override string GetFunctionEffectSummary(Player? owner, Creature? target) =>
        $"Gain [blue]{DynamicVars.Block.IntValue}[/blue] [gold]Block[/gold].";

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
        DynamicVars.Block.UpgradeValueBy(2m);
    }
}
