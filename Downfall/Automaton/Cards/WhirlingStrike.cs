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
/// Piercing Shot (WhirlingStrike, Common, 1 cost, Encodes):
/// Deal 6 (8) damage to ALL enemies.
/// </summary>
public sealed class WhirlingStrike() : BronzeCard(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies)
{
    public override bool Encodes => true;
    public override string FunctionAdjective => "piercing";
    public override string FunctionNoun => "Shot";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        EncodeHoverTip,
        FunctionHoverTip
    ];

    public override string GetFunctionEffectSummary(Player? owner, Creature? target) =>
        $"Deal [blue]{DynamicVars.Damage.IntValue}[/blue] damage to ALL enemies.";

    public override async Task ExecuteEffectAsync(
        PlayerChoiceContext choiceContext,
        Player owner,
        Creature? target,
        CardPlay? cardPlay,
        CardModel? sourceCard,
        bool isFromPower)
    {
        await DealAllEnemiesDamageAsync(choiceContext, owner, cardPlay, sourceCard, isFromPower, DynamicVars.Damage.BaseValue);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
