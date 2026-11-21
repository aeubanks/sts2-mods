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
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Automaton.Cards;

/// <summary>
/// Bronze Armor (Uncommon, 0 cost, Encodes, Compile Error):
/// Gain 1 Artifact. Compile Error - ALL enemies gain 12 (8) Block.
/// </summary>
public sealed class AutomatonBronzeArmor() : BronzeCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override bool Encodes => true;
    public override bool IsCompileError => true;
    public override string FunctionAdjective => "bronze";
    public override string FunctionNoun => "Armor";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<ArtifactPower>(1m),
        new IntVar("EnemyBlock", 12m)
    ];

    protected override IEnumerable<string> ExtraFineTunableVarNames => [nameof(ArtifactPower)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        EncodeHoverTip,
        CompileHoverTip,
        FunctionHoverTip,
        HoverTipFactory.FromPower<ArtifactPower>(),
        HoverTipFactory.Static(StaticHoverTip.Block)
    ];

    public override string GetFunctionEffectSummary(Player? owner, Creature? target) =>
        $"Gain [blue]{DynamicVars[nameof(ArtifactPower)].IntValue}[/blue] [gold]Artifact[/gold].";

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

        await PowerCmd.Apply<ArtifactPower>(
            choiceContext,
            owner.Creature,
            DynamicVars[nameof(ArtifactPower)].BaseValue,
            owner.Creature,
            isFromPower ? null : sourceCard);
    }

    public override async Task OnCompileAsync(PlayerChoiceContext choiceContext, Player player, FunctionCard function)
    {
        if (player.Creature.CombatState is not { } combatState || CombatManager.Instance.IsOverOrEnding)
            return;

        int enemyBlock = DynamicVars["EnemyBlock"].IntValue;
        if (enemyBlock <= 0)
            return;

        foreach (var enemy in combatState.HittableEnemies.Where(e => e.IsAlive).ToList())
        {
            if (CombatManager.Instance.IsOverOrEnding)
                break;
            await CreatureCmd.GainBlock(enemy, enemyBlock, ValueProp.Unpowered, null);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["EnemyBlock"].UpgradeValueBy(-4m);
    }
}
