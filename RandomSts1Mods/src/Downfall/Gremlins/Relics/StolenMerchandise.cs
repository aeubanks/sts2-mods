using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace Downfall.Gremlins.Relics;

public sealed class StolenMerchandise : GremlinRelic, IGremlinSwapListener
{
    public override RelicRarity Rarity => RelicRarity.Shop;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GremlinCard.SwapHoverTip,
        GremlinCard.EchoHoverTip
    ];

    public async Task OnGremlinSwappedAsync(PlayerChoiceContext choiceContext, GremlinKind previous, GremlinKind current)
    {
        if (!Owner.Creature.IsAlive
            || Owner.Creature.CombatState is not { } combatState
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        var pool = ModelDb.CardPool<ColorlessCardPool>().AllCards
            .Where(c => c.CanBeGeneratedInCombat && c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare)
            .ToList();
        if (pool.Count == 0)
            return;

        var template = pool[Owner.RunState.Rng.CombatCardGeneration.NextInt(pool.Count)];
        var created = combatState.CreateCard(template, Owner);
        if (GremlinMob.MakeEcho(created) is { } echo)
        {
            Flash();
            await CardPileCmd.AddGeneratedCardToCombat(echo, PileType.Hand, Owner);
        }
    }
}
