using Downfall.Gremlins.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Gremlins.Cards;

public sealed class GremlinDance()
    : GremlinCard(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
{
    private ICombatState? ActiveCombatState => CombatState ?? Owner?.Creature.CombatState;

    private (bool InCombat, bool IsNob, GremlinKind Kind) GetActiveMode()
    {
        if (!IsMutable
            || Owner is null
            || ActiveCombatState is null
            || !CombatManager.Instance.IsInProgress
            || Pile?.Type == PileType.Deck
            || !GremlinMob.TryGetCombatModeReadOnly(Owner, out bool isNob, out var kind))
        {
            return (false, false, GremlinKind.Mad);
        }

        return (true, isNob, kind);
    }

    public override TargetType TargetType
    {
        get
        {
            var (inCombat, isNob, kind) = GetActiveMode();
            return inCombat && !isNob && kind == GremlinKind.Mad ? TargetType.AllEnemies : TargetType.AnyEnemy;
        }
    }

    public override bool GainsBlock
    {
        get
        {
            var (inCombat, isNob, kind) = GetActiveMode();
            return inCombat && !isNob && kind == GremlinKind.Shield;
        }
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(6m, ValueProp.Move),
        new BlockVar(6m, ValueProp.Move),
        new DynamicVar("Bonus", 2m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            var (inCombat, isNob, kind) = GetActiveMode();
            if (!inCombat)
                yield break;

            if (isNob || kind == GremlinKind.Fat)
            {
                yield return HoverTipFactory.FromPower<StrengthPower>();
            }
            else if (kind == GremlinKind.Shield)
            {
                yield return HoverTipFactory.Static(StaticHoverTip.Block);
            }
            else if (kind == GremlinKind.Wizard)
            {
                yield return HoverTipFactory.FromPower<WizPower>();
            }
        }
    }

    protected override void AddExtraArgsToDescription(LocString description)
    {
        var (inCombat, isNob, kind) = GetActiveMode();
        description.Add("IsMad", inCombat && !isNob && kind == GremlinKind.Mad);
        description.Add("IsFat", inCombat && !isNob && kind == GremlinKind.Fat);
        description.Add("IsShield", inCombat && !isNob && kind == GremlinKind.Shield);
        description.Add("IsSneaky", inCombat && !isNob && kind == GremlinKind.Sneaky);
        description.Add("IsWizard", inCombat && !isNob && kind == GremlinKind.Wizard);
        description.Add("IsNob", inCombat && isNob);
        description.Add("IsNeutral", !inCombat);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is null)
            return;

        bool isNob = GremlinMob.IsNob(CombatState, Owner);
        GremlinKind kind = GremlinMob.GetActiveKind(CombatState, Owner);
        decimal bonus = DynamicVars["Bonus"].BaseValue;

        if (!isNob && kind == GremlinKind.Shield)
        {
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        }

        if (!isNob && kind == GremlinKind.Mad)
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                .FromCard(this, cardPlay)
                .TargetingAllOpponents(CombatState)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }
        else
        {
            var target = cardPlay.Target ?? CombatState.HittableEnemies.FirstOrDefault(e => e.IsAlive);
            if (target is not null)
            {
                await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
                    .FromCard(this, cardPlay)
                    .Targeting(target)
                    .WithHitFx("vfx/vfx_attack_slash")
                    .Execute(choiceContext);

                if (!isNob
                    && kind == GremlinKind.Fat
                    && target.IsAlive
                    && Owner.Creature.IsAlive
                    && !CombatManager.Instance.IsOverOrEnding)
                {
                    await PowerCmd.Apply<PiercingWailPower>(choiceContext, target, bonus, Owner.Creature, this);
                }
            }
        }

        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        if (isNob)
        {
            await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, bonus, Owner.Creature, this);
            return;
        }

        switch (kind)
        {
            case GremlinKind.Sneaky:
                await CardPileCmd.Draw(choiceContext, (int)bonus, Owner);
                break;
            case GremlinKind.Wizard:
                await PowerCmd.Apply<WizPower>(choiceContext, Owner.Creature, bonus, Owner.Creature, this);
                break;
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}
