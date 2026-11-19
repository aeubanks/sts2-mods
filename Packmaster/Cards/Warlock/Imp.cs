using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace Packmaster.Cards.Warlock;

public sealed class Imp()
    : PackmasterCard(-1, CardType.Skill, CardRarity.Token, TargetType.None)
{
    private const decimal BaseDamage = 3m;
    private static readonly ConditionalWeakTable<PlayerCombatState, StrongBox<int>> BonusByPlayer = new();

    protected override string PortraitKey => "imp";

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Unplayable];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("ImpDamage", BaseDamage),
        new DynamicVar("Increase", 1m)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
    ];

    private static int GetBonus(PlayerCombatState? pcs) =>
        pcs is not null && BonusByPlayer.TryGetValue(pcs, out var box) ? box.Value : 0;

    private void SyncDamage()
    {
        DynamicVars["ImpDamage"].BaseValue = BaseDamage + GetBonus(Owner?.PlayerCombatState);
    }

    public override Task AfterCardEnteredCombat(CardModel card)
    {
        if (card == this)
        {
            SyncDamage();
        }
        return Task.CompletedTask;
    }

    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card != this || !Owner.Creature.IsAlive || CombatState is null || Owner.PlayerCombatState is null)
            return;

        SyncDamage();
        decimal damage = DynamicVars["ImpDamage"].BaseValue;
        var target = Owner.RunState.Rng.CombatTargets.NextItem(CombatState.HittableEnemies);
        if (target is not null)
        {
            await CreatureCmd.Damage(
                choiceContext,
                target,
                damage,
                ValueProp.Unpowered,
                dealer: Owner.Creature,
                cardSource: this,
                cardPlay: null);
        }

        var bonusBox = BonusByPlayer.GetOrCreateValue(Owner.PlayerCombatState);
        bonusBox.Value += DynamicVars["Increase"].IntValue;
        foreach (var imp in Owner.PlayerCombatState.AllCards.OfType<Imp>())
        {
            imp.SyncDamage();
        }

        if (Pile?.Type == PileType.Hand)
        {
            await CardCmd.Exhaust(choiceContext, this);
        }

        if (Owner.Creature.IsAlive)
        {
            await CardPileCmd.Draw(choiceContext, 1m, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Increase"].UpgradeValueBy(1m);
    }
}
