using Downfall.Gremlins.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Gremlins.Relics;

public sealed class WizardHat : GremlinRelic, IGremlinWizConsumedListener
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<WizPower>()
    ];

    public async Task OnWizConsumedAsync(PlayerChoiceContext choiceContext)
    {
        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        var debuffs = Owner.Creature.Powers
            .Where(p => p.TypeForCurrentAmount == PowerType.Debuff)
            .ToList();
        if (debuffs.Count == 0)
            return;

        var chosen = debuffs[Owner.RunState.Rng.Niche.NextInt(debuffs.Count)];
        Flash();
        await PowerCmd.Remove(chosen);
    }
}
