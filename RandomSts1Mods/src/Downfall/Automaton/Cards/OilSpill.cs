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
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Automaton.Cards;

/// <summary>
/// Oil Spill (Common, 1 cost, Encodes, Compile Error):
/// Deal 4 (5) damage and apply 4 (5) Poison. Compile Error - Insert a Slimed.
/// </summary>
public sealed class OilSpill() : BronzeCard(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    public override bool Encodes => true;
    public override bool IsCompileError => true;
    public override string FunctionAdjective => "oily";
    public override string FunctionNoun => "Spill";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4m, ValueProp.Move),
        new PowerVar<PoisonPower>(4m)
    ];

    protected override IEnumerable<string> ExtraFineTunableVarNames => [nameof(PoisonPower)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        EncodeHoverTip,
        CompileHoverTip,
        InsertHoverTip,
        FunctionHoverTip,
        HoverTipFactory.FromPower<PoisonPower>(),
        HoverTipFactory.FromCard<Slimed>()
    ];

    public override string GetFunctionEffectSummary(Player? owner, Creature? target) =>
        $"Deal [blue]{DynamicVars.Damage.IntValue}[/blue] damage and apply [blue]{DynamicVars[nameof(PoisonPower)].IntValue}[/blue] [gold]Poison[/gold].";

    public override async Task ExecuteEffectAsync(
        PlayerChoiceContext choiceContext,
        Player owner,
        Creature? target,
        CardPlay? cardPlay,
        CardModel? sourceCard,
        bool isFromPower)
    {
        if (CombatManager.Instance.IsOverOrEnding)
            return;

        var resolved = ResolveTarget(owner, target);
        if (resolved is null)
            return;

        await DealDamageAsync(choiceContext, owner, resolved, cardPlay, sourceCard, isFromPower, DynamicVars.Damage.BaseValue);
        if (resolved.IsAlive && owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Apply<PoisonPower>(
                choiceContext,
                resolved,
                DynamicVars[nameof(PoisonPower)].BaseValue,
                owner.Creature,
                isFromPower ? null : sourceCard);
        }
    }

    public override async Task OnCompileAsync(PlayerChoiceContext choiceContext, Player player, FunctionCard function)
    {
        await AutomatonMechanics.InsertCardIntoDrawPileAsync<Slimed>(player, 1);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(1m);
        DynamicVars[nameof(PoisonPower)].UpgradeValueBy(1m);
    }
}
