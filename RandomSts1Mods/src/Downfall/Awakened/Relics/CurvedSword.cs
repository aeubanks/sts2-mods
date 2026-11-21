using Downfall.Awakened.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Rooms;

namespace Downfall.Awakened.Relics;

public sealed class CurvedSword : AwakenedRelic
{
    private const int SpellsRequired = 4;

    private int _spellsPlayed;
    private bool _inCombat;

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override bool ShowCounter => _inCombat;
    public override int DisplayAmount => _spellsPlayed;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Spells", SpellsRequired)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<Ceremony>()
    ];

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _spellsPlayed = 0;
        _inCombat = false;
    }

    private void ResetCombatState(bool inCombat)
    {
        _inCombat = inCombat;
        _spellsPlayed = 0;
        InvokeDisplayAmountChanged();
    }

    public override Task AfterObtained()
    {
        if (CombatManager.Instance.IsInProgress)
        {
            ResetCombatState(true);
        }
        return Task.CompletedTask;
    }

    public override Task BeforeCombatStart()
    {
        ResetCombatState(true);
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        ResetCombatState(false);
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await base.AfterCardPlayed(choiceContext, cardPlay);

        if (cardPlay.Card.Owner != Owner
            || cardPlay.Card is not AwakenedSpellCard
            || !Owner.Creature.IsAlive
            || Owner.Creature.CombatState is not { } combatState
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        _spellsPlayed++;
        if (_spellsPlayed >= SpellsRequired)
        {
            _spellsPlayed = 0;
            Flash();
            var ceremony = combatState.CreateCard<Ceremony>(Owner);
            await CardPileCmd.AddGeneratedCardToCombat(ceremony, PileType.Hand, Owner);
        }

        InvokeDisplayAmountChanged();
    }
}
