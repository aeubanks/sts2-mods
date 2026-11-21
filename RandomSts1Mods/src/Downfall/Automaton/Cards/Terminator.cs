using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Automaton.Cards;

/// <summary>
/// Terminator (Uncommon, 1 -> 0 cost, Encodes):
/// Compile - If this is the last card of the Function (slot 2), it gains 'Play this again'.
/// </summary>
public sealed class Terminator() : BronzeCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override bool Encodes => true;
    public override string FunctionAdjective => "sad";
    public override string FunctionNoun => "End";

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        EncodeHoverTip,
        CompileHoverTip,
        FunctionHoverTip
    ];

    public override Task OnCompileAsync(PlayerChoiceContext choiceContext, Player player, FunctionCard function)
    {
        if (IsLastInSequence)
            function.SetPlayTwice();
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
