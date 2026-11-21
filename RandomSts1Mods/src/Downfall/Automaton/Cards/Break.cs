using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Automaton.Cards;

/// <summary>
/// Break (Rare, 1 cost, Encodes, Compile Error):
/// Deal 15 (20) damage. Compile Error - Add a Dazed, Slimed, Wound, Burn, and Void to your Hand.
/// </summary>
public sealed class Break() : BronzeCard(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
    public override bool Encodes => true;
    public override bool IsCompileError => true;
    public override string FunctionAdjective => "broken";
    public override string FunctionNoun => "Smash";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(15m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        EncodeHoverTip,
        CompileHoverTip,
        FunctionHoverTip,
        HoverTipFactory.FromCard<Dazed>(),
        HoverTipFactory.FromCard<Slimed>(),
        HoverTipFactory.FromCard<Wound>(),
        HoverTipFactory.FromCard<Burn>(),
        HoverTipFactory.FromCard<MegaCrit.Sts2.Core.Models.Cards.Void>()
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
        if (player.Creature.CombatState is not { } combatState
            || !player.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        CardModel[] statuses =
        [
            combatState.CreateCard<Dazed>(player),
            combatState.CreateCard<Slimed>(player),
            combatState.CreateCard<Wound>(player),
            combatState.CreateCard<Burn>(player),
            combatState.CreateCard<MegaCrit.Sts2.Core.Models.Cards.Void>(player)
        ];

        foreach (var status in statuses)
        {
            if (!player.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;
            await AutomatonMechanics.AddGeneratedCardToHandOrDiscardAsync(status, player);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(5m);
    }
}
