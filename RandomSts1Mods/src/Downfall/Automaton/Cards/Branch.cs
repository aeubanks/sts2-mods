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
/// Branch (Common, 1 cost, Exhaust):
/// Deal 7 (9) damage or gain 6 (8) Block. The option not chosen is Encoded.
/// </summary>
public sealed class Branch() : BronzeCard(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    public override bool GainsBlock => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(7m, ValueProp.Move),
        new BlockVar(6m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        EncodeHoverTip,
        FunctionHoverTip
    ];

    public override async Task ExecuteEffectAsync(
        PlayerChoiceContext choiceContext,
        Player owner,
        Creature? target,
        CardPlay? cardPlay,
        CardModel? sourceCard,
        bool isFromPower)
    {
        if (owner.Creature.CombatState is not { } combatState
            || !owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        var hitChoice = (BranchHit)combatState.CreateCard<BranchHit>(owner);
        var blockChoice = (BranchBlock)combatState.CreateCard<BranchBlock>(owner);
        if (IsUpgraded)
        {
            CardCmd.Upgrade(hitChoice);
            CardCmd.Upgrade(blockChoice);
        }

        var chosen = await CardSelectCmd.FromChooseACardScreen(choiceContext, [hitChoice, blockChoice], owner, canSkip: false);
        if (!owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        if (chosen is BranchBlock)
        {
            await GainBlockAsync(owner, cardPlay, isFromPower, DynamicVars.Block.BaseValue);
            if (owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
                await AutomatonMechanics.EncodeCardAsync(choiceContext, owner, hitChoice);
        }
        else
        {
            await DealDamageAsync(choiceContext, owner, target, cardPlay, sourceCard, isFromPower, DynamicVars.Damage.BaseValue);
            if (owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
                await AutomatonMechanics.EncodeCardAsync(choiceContext, owner, blockChoice);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars.Block.UpgradeValueBy(2m);
    }
}

public sealed class BranchHit() : BronzeTokenCard(1, CardType.Attack, CardRarity.Token, TargetType.AnyEnemy)
{
    public override bool Encodes => true;
    public override string FunctionAdjective => "forked";
    public override string FunctionNoun => "Branch";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(7m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        EncodeHoverTip,
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

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}

public sealed class BranchBlock() : BronzeTokenCard(1, CardType.Skill, CardRarity.Token, TargetType.Self)
{
    public override bool Encodes => true;
    public override bool GainsBlock => true;
    public override string FunctionAdjective => "forked";
    public override string FunctionNoun => "Branch";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(6m, ValueProp.Move)
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
