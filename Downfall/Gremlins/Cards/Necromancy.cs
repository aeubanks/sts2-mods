using Downfall.Gremlins.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Gremlins.Cards;

public sealed class Necromancy()
    : GremlinCard(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override bool CanBeGeneratedInCombat => false;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override bool IsPlayable =>
        IsMutable && Owner?.Creature.GetPowerAmount<WizPower>() >= WizPower.Threshold;

    protected override bool ShouldGlowGoldInternal => IsPlayable;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new HealVar(15m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<WizPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.Creature.GetPower<WizPower>() is { } wiz)
        {
            await wiz.ConsumeAsync(choiceContext);
        }

        if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            await CreatureCmd.Heal(Owner.Creature, DynamicVars.Heal.BaseValue);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Heal.UpgradeValueBy(5m);
    }
}
