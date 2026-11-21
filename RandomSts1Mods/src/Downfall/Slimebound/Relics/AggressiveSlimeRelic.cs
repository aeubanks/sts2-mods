using Downfall.Slimebound.Orbs;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Slimebound.Relics;

public sealed class AggressiveSlimeRelic : SlimeRelic
{
    public override RelicRarity Rarity => RelicRarity.Common;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        SlimeCard.SplitHoverTip,
        HoverTipFactory.FromOrb<AttackSlime>()
    ];

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner
            || Owner.Creature.CombatState?.RoundNumber != 1
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        await SlimeMechanics.SplitSlimeAsync<AttackSlime>(choiceContext, Owner);
    }
}
