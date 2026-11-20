using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
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
/// Cut Through (Common, 1 cost, Encodes):
/// Deal 5 (7) damage. Scry 2 (3). Compile - Draw 1 card.
/// </summary>
public sealed class CutThrough() : BronzeCard(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    public override bool Encodes => true;
    public override string FunctionAdjective => "cutting";
    public override string FunctionNoun => "Cut";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(5m, ValueProp.Move),
        new IntVar("Scry", 2m),
        new IntVar("CompileDraw", 1m)
    ];

    protected override IEnumerable<string> ExtraFineTunableVarNames => ["Scry", "CompileDraw"];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        EncodeHoverTip,
        CompileHoverTip,
        FunctionHoverTip
    ];

    public override string GetFunctionEffectSummary(Player? owner, Creature? target) =>
        $"Deal [blue]{DynamicVars.Damage.IntValue}[/blue] damage. Scry [blue]{DynamicVars["Scry"].IntValue}[/blue].";

    public override async Task ExecuteEffectAsync(
        PlayerChoiceContext choiceContext,
        Player owner,
        Creature? target,
        CardPlay? cardPlay,
        CardModel? sourceCard,
        bool isFromPower)
    {
        await DealDamageAsync(choiceContext, owner, target, cardPlay, sourceCard, isFromPower, DynamicVars.Damage.BaseValue);
        if (owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            await AutomatonMechanics.ScryAsync(choiceContext, owner, DynamicVars["Scry"].IntValue, SelectionScreenPrompt);
    }

    public override async Task OnCompileAsync(PlayerChoiceContext choiceContext, Player player, FunctionCard function)
    {
        if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        await CardPileCmd.Draw(choiceContext, DynamicVars["CompileDraw"].BaseValue, player);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars["Scry"].UpgradeValueBy(1m);
    }
}
