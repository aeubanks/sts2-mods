using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;

namespace Downfall.Champ.Relics;

public sealed class BlackKnightsHelmet : ChampRelic, IChampStanceChangedListener
{
    private bool _didDefensive;
    private bool _didBerserker;

    public override RelicRarity Rarity => RelicRarity.Event;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        ChampCard.DefensiveHoverTip,
        ChampCard.BerserkerHoverTip,
        HoverTipFactory.FromPower<StrengthPower>(),
        HoverTipFactory.FromPower<DexterityPower>()
    ];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _didDefensive = false;
        _didBerserker = false;
    }

    public override Task BeforeCombatStart()
    {
        _didDefensive = false;
        _didBerserker = false;
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _didDefensive = false;
        _didBerserker = false;
        return Task.CompletedTask;
    }

    public async Task OnStanceChangedAsync(
        PlayerChoiceContext choiceContext,
        ChampStanceKind previous,
        ChampStanceKind current)
    {
        if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
            return;

        if (current == ChampStanceKind.Defensive && !_didDefensive)
        {
            _didDefensive = true;
            Flash();
            await PowerCmd.Apply<DexterityPower>(choiceContext, Owner.Creature, 2m, Owner.Creature, null);
            if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            {
                await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, -1m, Owner.Creature, null);
            }
        }
        else if (current == ChampStanceKind.Berserker && !_didBerserker)
        {
            _didBerserker = true;
            Flash();
            await PowerCmd.Apply<StrengthPower>(choiceContext, Owner.Creature, 2m, Owner.Creature, null);
            if (Owner.Creature.IsAlive && !CombatManager.Instance.IsOverOrEnding)
            {
                await PowerCmd.Apply<DexterityPower>(choiceContext, Owner.Creature, -1m, Owner.Creature, null);
            }
        }
    }
}
