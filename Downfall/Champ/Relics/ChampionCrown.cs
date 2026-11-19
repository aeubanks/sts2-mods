using Downfall.Champ.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Rooms;

namespace Downfall.Champ.Relics;

public sealed class ChampionCrown : ChampRelic
{
    private bool _triggeredThisCombat;

    public override RelicRarity Rarity => RelicRarity.Starter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<StanceDanceCrown>(),
        ChampCard.BerserkerHoverTip,
        ChampCard.DefensiveHoverTip
    ];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _triggeredThisCombat = false;
    }

    public override Task BeforeCombatStart()
    {
        _triggeredThisCombat = false;
        Status = RelicStatus.Normal;
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _triggeredThisCombat = false;
        Status = RelicStatus.Normal;
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (_triggeredThisCombat
            || player != Owner
            || Owner.Creature.CombatState is not { RoundNumber: 1 } combatState
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        _triggeredThisCombat = true;
        Status = RelicStatus.Disabled;
        Flash();

        var crown = combatState.CreateCard<StanceDanceCrown>(Owner);
        await CardPileCmd.AddGeneratedCardToCombat(crown, PileType.Hand, Owner);
    }
}
