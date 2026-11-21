using Downfall.Awakened.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace Downfall.Awakened.Relics;

public sealed class ZenerDeck : AwakenedRelic
{
    private bool _triggeredThisCombat;

    public override RelicRarity Rarity => RelicRarity.Rare;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        AwakenedCard.ConjureHoverTip,
        HoverTipFactory.FromCard<EspSpell>()
    ];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _triggeredThisCombat = false;
    }

    public override Task BeforeCombatStart()
    {
        _triggeredThisCombat = false;
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _triggeredThisCombat = false;
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner
            || _triggeredThisCombat
            || !Owner.Creature.IsAlive
            || Owner.Creature.CombatState is not { } combatState
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        _triggeredThisCombat = true;
        Flash();
        await AwakenedSpellbook.ConjureSpecificAndRegisterAsync(
            Owner,
            combatState,
            ModelDb.Card<EspSpell>());
    }
}
