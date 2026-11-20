using Downfall.Hexaghost.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace Downfall.Hexaghost.Cards;

/// <summary>
/// Living Bomb (SoulSteal): Apply 4 (7) Soulburn. When it detonates, it affects ALL enemies.
/// </summary>
public sealed class SoulSteal() : HexaCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Soulburn", 4m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        SoulburnHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await ApplySoulburnAsync(choiceContext, cardPlay.Target, DynamicVars["Soulburn"].IntValue);
        if (cardPlay.Target.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            await PowerCmd.Apply<LivingBombPower>(choiceContext, cardPlay.Target, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Soulburn"].UpgradeValueBy(3m);
    }
}
