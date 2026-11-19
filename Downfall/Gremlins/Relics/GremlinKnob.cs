using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Rooms;

namespace Downfall.Gremlins.Relics;

public sealed class GremlinKnob : GremlinRelic
{
    private bool _triggeredThisCombat;

    public override RelicRarity Rarity => RelicRarity.Starter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(1),
        new CardsVar(1)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.ForEnergy(this),
        GremlinCard.SwapHoverTip
    ];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _triggeredThisCombat = false;
    }

    public override async Task BeforeCombatStart()
    {
        _triggeredThisCombat = false;
        Status = RelicStatus.Normal;
        if (Owner.Creature.CombatState is { } combatState)
        {
            await GremlinMob.EnsureInitializedAsync(combatState, Owner, new ThrowingPlayerChoiceContext());
        }
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        _triggeredThisCombat = false;
        Status = RelicStatus.Normal;
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner
            || _triggeredThisCombat
            || Owner.Creature.CombatState is not { RoundNumber: 1 } combatState
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        _triggeredThisCombat = true;
        Status = RelicStatus.Disabled;
        Flash();

        await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, Owner);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
        await GremlinMob.SwapNextAsync(Owner, combatState, choiceContext);
    }
}
