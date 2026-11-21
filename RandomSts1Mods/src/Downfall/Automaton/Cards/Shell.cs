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

namespace Downfall.Automaton.Cards;

/// <summary>
/// Blockchain (Shell, Uncommon, 1 cost, Encodes):
/// Gain 1 Blur. Upgraded: Compile - Gain 1 Blur.
/// </summary>
public sealed class Shell() : BronzeCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override bool Encodes => true;
    public override string FunctionAdjective => "block";
    public override string FunctionNoun => "Chain";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<BlurPower>(1m)
    ];

    protected override IEnumerable<string> ExtraFineTunableVarNames => [nameof(BlurPower)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => IsUpgraded
        ? [EncodeHoverTip, CompileHoverTip, FunctionHoverTip, HoverTipFactory.FromPower<BlurPower>()]
        : [EncodeHoverTip, FunctionHoverTip, HoverTipFactory.FromPower<BlurPower>()];

    public override string GetFunctionEffectSummary(Player? owner, Creature? target) =>
        $"Gain [blue]{DynamicVars[nameof(BlurPower)].IntValue}[/blue] [gold]Blur[/gold].";

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

        await PowerCmd.Apply<BlurPower>(
            choiceContext,
            owner.Creature,
            DynamicVars[nameof(BlurPower)].BaseValue,
            owner.Creature,
            isFromPower ? null : sourceCard);
    }

    public override async Task OnCompileAsync(PlayerChoiceContext choiceContext, Player player, FunctionCard function)
    {
        if (!IsUpgraded || !player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        await PowerCmd.Apply<BlurPower>(
            choiceContext,
            player.Creature,
            DynamicVars[nameof(BlurPower)].BaseValue,
            player.Creature,
            null);
    }
}
