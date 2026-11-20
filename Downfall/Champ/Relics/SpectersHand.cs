using Downfall.Champ.Cards;
using Downfall.Core;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace Downfall.Champ.Relics;

public sealed class SpectersHand : ChampRelic, IChampStanceChangedListener
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        DownfallEcho.EchoHoverTip,
        HoverTipFactory.FromCard<ChampStrike>(),
        HoverTipFactory.FromCard<ChampDefend>()
    ];

    public async Task OnStanceChangedAsync(
        PlayerChoiceContext choiceContext,
        ChampStanceKind previous,
        ChampStanceKind current)
    {
        if (current == ChampStanceKind.None
            || previous == current
            || Owner.Creature.CombatState is not { } combatState
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        Flash();
        CardModel created = Owner.RunState.Rng.CombatCardGeneration.NextInt(2) == 0
            ? combatState.CreateCard<ChampStrike>(Owner)
            : combatState.CreateCard<ChampDefend>(Owner);

        created.EnergyCost.SetThisCombat(0, reduceOnly: true);
        if (DownfallEcho.MakeEcho(created) is { } echo)
        {
            await CardPileCmd.AddGeneratedCardToCombat(echo, PileType.Hand, Owner);
        }
    }
}
