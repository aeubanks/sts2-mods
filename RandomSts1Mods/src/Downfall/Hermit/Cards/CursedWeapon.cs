using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Hermit.Cards;

public sealed class CursedWeapon()
    : HermitCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    private int _bonusDamage;

    protected override string PortraitKey => "cursed_weapon";

    public override bool CanBeGeneratedInCombat => false;

    public override HashSet<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    [SavedProperty]
    public int BonusDamage
    {
        get => _bonusDamage;
        set
        {
            AssertMutable();
            _bonusDamage = value;
            DynamicVars.Damage.BaseValue = 10m + _bonusDamage;
        }
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(10m + _bonusDamage, ValueProp.Move),
        new HpLossVar(2m),
        new IntVar("DamageIncrease", 1m)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        await CreatureCmd.Damage(
            choiceContext,
            Owner.Creature,
            DynamicVars.HpLoss.BaseValue,
            ValueProp.Unblockable | ValueProp.Unpowered,
            dealer: null,
            cardSource: this,
            cardPlay: cardPlay);

        if (!Owner.Creature.IsAlive)
            return;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        int inc = DynamicVars["DamageIncrease"].IntValue;
        // Permanently buff all CursedWeapons in combat and in the player's master deck.
        var buffed = new HashSet<CursedWeapon>(ReferenceEqualityComparer.Instance);
        void Buff(CursedWeapon cw)
        {
            if (buffed.Add(cw))
            {
                cw.BonusDamage += inc;
            }
        }

        Buff(this);
        if (Owner.PlayerCombatState is { } pcs)
        {
            foreach (var c in pcs.AllCards.OfType<CursedWeapon>())
                Buff(c);
        }
        foreach (var deckCard in Owner.Deck.Cards.OfType<CursedWeapon>())
            Buff(deckCard);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
