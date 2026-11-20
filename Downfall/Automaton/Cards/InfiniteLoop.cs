using MegaCrit.Sts2.Core.Combat;
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
/// Infinite Loop (Uncommon, 1 cost, Encodes):
/// Deal 6 damage. Compile - Add a copy of this to your Hand and increase its damage by 2 (4).
/// </summary>
public sealed class InfiniteLoop() : BronzeCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override bool Encodes => true;
    public override string FunctionAdjective => "infinite";
    public override string FunctionNoun => "Loop";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6m, ValueProp.Move),
        new IntVar("Bonus", 2m)
    ];

    protected override IEnumerable<string> ExtraFineTunableVarNames => ["Bonus"];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        EncodeHoverTip,
        CompileHoverTip,
        FunctionHoverTip
    ];

    public override string GetFunctionEffectSummary(Player? owner, Creature? target) =>
        $"Deal [blue]{DynamicVars.Damage.IntValue}[/blue] damage.";

    public override async Task ExecuteEffectAsync(
        PlayerChoiceContext choiceContext,
        Player owner,
        Creature? target,
        CardPlay? cardPlay,
        CardModel? sourceCard,
        bool isFromPower)
    {
        await DealDamageAsync(choiceContext, owner, target, cardPlay, sourceCard, isFromPower, DynamicVars.Damage.BaseValue);
    }

    public override async Task OnCompileAsync(PlayerChoiceContext choiceContext, Player player, FunctionCard function)
    {
        if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var copy = (InfiniteLoop)CreateClone();
        copy.DynamicVars.Damage.BaseValue += DynamicVars["Bonus"].IntValue;
        await AutomatonMechanics.AddGeneratedCardToHandOrDiscardAsync(copy, player);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Bonus"].UpgradeValueBy(2m);
    }
}
