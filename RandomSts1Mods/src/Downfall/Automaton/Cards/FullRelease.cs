using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Automaton.Cards;

/// <summary>
/// Full Release (Rare, 1 -> 0 cost, Encodes):
/// Compile - Function becomes a Power with "At the start of your turn..."
/// Cannot be played while another Full Release is already Encoded.
/// </summary>
public sealed class FullRelease() : BronzeCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override bool Encodes => true;
    public override string FunctionAdjective => "full";
    public override string FunctionNoun => "Release";
    public override string FunctionBonusChar => "1.0";

    protected override bool IsPlayable =>
        !IsMutable || Owner is null || !AutomatonMechanics.GetEncodedCards(Owner).Any(c => c is FullRelease);

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        EncodeHoverTip,
        CompileHoverTip,
        FunctionHoverTip
    ];

    public override string GetFunctionEffectSummary(Player? owner, Creature? target) => string.Empty;

    public override Task ExecuteEffectAsync(
        PlayerChoiceContext choiceContext,
        Player owner,
        Creature? target,
        CardPlay? cardPlay,
        CardModel? sourceCard,
        bool isFromPower) => Task.CompletedTask;

    public override Task OnCompileAsync(PlayerChoiceContext choiceContext, Player player, FunctionCard function)
    {
        function.SetFullRelease();
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
