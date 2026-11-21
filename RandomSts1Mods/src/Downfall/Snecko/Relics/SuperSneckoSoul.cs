using Downfall.Snecko.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Rooms;

namespace Downfall.Snecko.Relics;

public sealed class SuperSneckoSoul : SneckoRelic
{
    private bool _nextTurnDraws;

    public override RelicRarity Rarity => RelicRarity.Starter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<SoulRoll>(),
        SneckoCard.MuddleHoverTip
    ];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _nextTurnDraws = false;
    }

    public override async Task AfterObtained()
    {
        if (Owner.Relics.OfType<SneckoSoul>().FirstOrDefault() is { } oldSoul)
        {
            await RelicCmd.Remove(oldSoul);
        }
        _nextTurnDraws = false;
    }

    public override Task BeforeCombatStart()
    {
        _nextTurnDraws = false;
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _nextTurnDraws = false;
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding
            || Owner.Creature.CombatState is not { } combatState)
            return;

        Flash();
        if (!_nextTurnDraws)
        {
            _nextTurnDraws = true;
            var soulRoll = combatState.CreateCard<SoulRoll>(Owner);
            await CardPileCmd.AddGeneratedCardToCombat(soulRoll, PileType.Hand, Owner);
        }
        else
        {
            _nextTurnDraws = false;
            var drawn = (await CardPileCmd.Draw(choiceContext, 1m, Owner)).ToList();
            foreach (var card in drawn)
            {
                await SneckoMechanics.MuddleAsync(choiceContext, Owner, card);
            }
        }
    }
}
