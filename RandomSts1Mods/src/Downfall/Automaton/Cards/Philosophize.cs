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
/// Philosophize (Uncommon, 1 cost, Encodes, Compile Error):
/// Gain 1 Strength. Compile Error - ALL enemies gain 2 (1) Strength.
/// </summary>
public sealed class Philosophize() : BronzeCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override bool Encodes => true;
    public override bool IsCompileError => true;
    public override string FunctionAdjective => "smart";
    public override string FunctionNoun => "Idea";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<StrengthPower>(1m),
        new IntVar("EnemyStrength", 2m)
    ];

    protected override IEnumerable<string> ExtraFineTunableVarNames => [nameof(StrengthPower)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        EncodeHoverTip,
        CompileHoverTip,
        FunctionHoverTip,
        HoverTipFactory.FromPower<StrengthPower>()
    ];

    public override string GetFunctionEffectSummary(Player? owner, Creature? target) =>
        $"Gain [blue]{DynamicVars[nameof(StrengthPower)].IntValue}[/blue] [gold]Strength[/gold].";

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

        await PowerCmd.Apply<StrengthPower>(
            choiceContext,
            owner.Creature,
            DynamicVars[nameof(StrengthPower)].BaseValue,
            owner.Creature,
            isFromPower ? null : sourceCard);
    }

    public override async Task OnCompileAsync(PlayerChoiceContext choiceContext, Player player, FunctionCard function)
    {
        if (player.Creature.CombatState is not { } combatState || CombatManager.Instance.IsOverOrEnding)
            return;

        int enemyStr = DynamicVars["EnemyStrength"].IntValue;
        if (enemyStr <= 0)
            return;

        foreach (var enemy in combatState.HittableEnemies.Where(e => e.IsAlive).ToList())
        {
            if (CombatManager.Instance.IsOverOrEnding)
                break;
            await PowerCmd.Apply<StrengthPower>(choiceContext, enemy, enemyStr, player.Creature, null);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["EnemyStrength"].UpgradeValueBy(-1m);
    }
}
