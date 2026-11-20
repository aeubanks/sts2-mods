using Downfall.Core;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.HoverTips;

namespace Downfall.Champ.Powers;

public sealed class StrikeOfGeniusPower : ChampPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        DownfallEcho.EchoHoverTip
    ];

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (side == Owner.Side && Owner.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            Flash();
        }
        await SpawnStrikeEchoesAsync(this, side, combatState, upgrade: false);
    }

    internal static async Task SpawnStrikeEchoesAsync(
        ChampPower power,
        CombatSide side,
        ICombatState combatState,
        bool upgrade)
    {
        if (side != power.Owner.Side
            || power.Owner.Player is not { } player
            || !power.Owner.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        var pool = player.Character.CardPool.AllCards
            .Where(c => c.Type == CardType.Attack
                && c.Tags.Contains(CardTag.Strike)
                && c.CanBeGeneratedInCombat
                && c.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare)
            .ToList();

        if (pool.Count == 0)
        {
            pool = player.Character.CardPool.AllCards
                .Where(c => c.Type == CardType.Attack && c.Tags.Contains(CardTag.Strike))
                .ToList();
        }

        if (pool.Count == 0)
            return;

        for (int i = 0; i < power.Amount; i++)
        {
            if (!power.Owner.IsAlive || CombatManager.Instance.IsOverOrEnding)
                break;

            var template = pool[player.RunState.Rng.CombatCardGeneration.NextInt(pool.Count)];
            var created = combatState.CreateCard(template, player);
            if (upgrade && created.IsUpgradable)
            {
                CardCmd.Upgrade(created);
            }
            created.EnergyCost.SetThisTurnOrUntilPlayed(0, reduceOnly: true);
            if (DownfallEcho.MakeEcho(created) is { } echo)
            {
                await CardPileCmd.AddGeneratedCardToCombat(echo, PileType.Hand, player);
            }
        }
    }
}
