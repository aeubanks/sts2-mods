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
/// Iterate (Uncommon, 1 cost, Encodes): Deal 2 damage 3 (4) times.
/// </summary>
public sealed class Iterate() : BronzeCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override bool Encodes => true;
    public override string FunctionAdjective => "parsed";
    public override string FunctionNoun => "Iterate";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(2m, ValueProp.Move),
        new IntVar("Hits", 3m)
    ];

    protected override IEnumerable<string> ExtraFineTunableVarNames => ["Hits"];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        EncodeHoverTip,
        FunctionHoverTip
    ];

    public override string GetFunctionEffectSummary(Player? owner, Creature? target) =>
        $"Deal [blue]{DynamicVars.Damage.IntValue}[/blue] damage [blue]{DynamicVars["Hits"].IntValue}[/blue] times.";

    public override async Task ExecuteEffectAsync(
        PlayerChoiceContext choiceContext,
        Player owner,
        Creature? target,
        CardPlay? cardPlay,
        CardModel? sourceCard,
        bool isFromPower)
    {
        await DealDamageAsync(
            choiceContext,
            owner,
            target,
            cardPlay,
            sourceCard,
            isFromPower,
            DynamicVars.Damage.BaseValue,
            DynamicVars["Hits"].IntValue);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Hits"].UpgradeValueBy(1m);
    }
}
