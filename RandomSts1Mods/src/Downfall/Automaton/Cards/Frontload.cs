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
/// Frontload (Common, 2 cost, Encodes):
/// Gain 8 (11) Block. Compile - Function gains Retain.
/// </summary>
public sealed class Frontload() : BronzeCard(2, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    public override bool Encodes => true;
    public override bool GainsBlock => true;
    public override string FunctionAdjective => "sturdy";
    public override string FunctionNoun => "Wall";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(8m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        EncodeHoverTip,
        CompileHoverTip,
        FunctionHoverTip,
        HoverTipFactory.FromKeyword(CardKeyword.Retain)
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

    public override Task OnCompileAsync(PlayerChoiceContext choiceContext, Player player, FunctionCard function)
    {
        function.AddKeyword(CardKeyword.Retain);
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}
