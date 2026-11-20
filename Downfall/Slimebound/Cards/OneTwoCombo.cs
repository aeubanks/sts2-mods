using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Slimebound.Cards;

public sealed class OneTwoCombo() : SlimeCard(0, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy), ISlimeSplitListener
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        CommandHoverTip,
        SplitHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        await SlimeMechanics.CommandAsync(choiceContext, Owner);
    }

    public async Task OnSlimeSplitAsync(PlayerChoiceContext choiceContext, SlimeOrb slime)
    {
        if (Pile?.Type != PileType.Discard
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding
            || Owner.PlayerCombatState is not { } pcs
            || pcs.Hand.Cards.Count >= 10)
        {
            return;
        }

        await CardPileCmd.Add(this, PileType.Hand);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
    }
}
