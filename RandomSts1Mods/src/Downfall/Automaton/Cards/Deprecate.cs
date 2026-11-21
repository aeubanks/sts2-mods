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
/// Deprecate (Common, 0 cost, Encodes): Apply 1 (2) Weak.
/// </summary>
public sealed class Deprecate() : BronzeCard(0, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy)
{
    public override bool Encodes => true;
    public override string FunctionAdjective => "old";
    public override string FunctionNoun => "Insult";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<WeakPower>(1m)
    ];

    protected override IEnumerable<string> ExtraFineTunableVarNames => [nameof(WeakPower)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        EncodeHoverTip,
        FunctionHoverTip,
        HoverTipFactory.FromPower<WeakPower>()
    ];

    public override string GetFunctionEffectSummary(Player? owner, Creature? target) =>
        $"Apply [blue]{DynamicVars[nameof(WeakPower)].IntValue}[/blue] [gold]Weak[/gold].";

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

        await PowerCmd.Apply<WeakPower>(
            choiceContext,
            resolved,
            DynamicVars[nameof(WeakPower)].BaseValue,
            owner.Creature,
            isFromPower ? null : sourceCard);
    }

    protected override void OnUpgrade()
    {
        DynamicVars[nameof(WeakPower)].UpgradeValueBy(1m);
    }
}
