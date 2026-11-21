using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace Downfall.Guardian.Cards;

[Pool(typeof(TokenCardPool))]
public abstract class GuardianPackageCard(int cost)
    : GuardianCard(cost, CardType.Skill, CardRarity.Token, TargetType.Self, showInCardLibrary: false)
{
    public override bool CanBeGeneratedInCombat => false;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected abstract IEnumerable<CardModel> CreatePackageCards();

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState is null)
            return;

        foreach (var card in CreatePackageCards())
        {
            if (!Owner.Creature.IsAlive || CombatManager.Instance.IsOverOrEnding)
                return;

            if (IsUpgraded && card.IsUpgradable)
                CardCmd.Upgrade(card);

            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner);
        }
    }
}

public sealed class PackageAutomaton() : GuardianPackageCard(1)
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<GigaBeam>(IsUpgraded),
        HoverTipFactory.FromCard<BronzeOrb>(IsUpgraded),
        HoverTipFactory.FromCard<BronzeArmor>(IsUpgraded)
    ];

    protected override IEnumerable<CardModel> CreatePackageCards() =>
    [
        CombatState!.CreateCard<GigaBeam>(Owner),
        CombatState.CreateCard<BronzeOrb>(Owner),
        CombatState.CreateCard<BronzeArmor>(Owner)
    ];
}

public sealed class PackageDonuDeca() : GuardianPackageCard(2)
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<DonusPower>(IsUpgraded),
        HoverTipFactory.FromCard<DecasProtection>(IsUpgraded),
        HoverTipFactory.FromCard<AncientConstruct>(IsUpgraded)
    ];

    protected override IEnumerable<CardModel> CreatePackageCards() =>
    [
        CombatState!.CreateCard<DonusPower>(Owner),
        CombatState.CreateCard<DecasProtection>(Owner),
        CombatState.CreateCard<AncientConstruct>(Owner)
    ];
}

public sealed class PackageSentry() : GuardianPackageCard(1)
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<SentryBeam>(IsUpgraded),
        HoverTipFactory.FromCard<SentryWave>(IsUpgraded)
    ];

    protected override IEnumerable<CardModel> CreatePackageCards() =>
    [
        CombatState!.CreateCard<SentryBeam>(Owner),
        CombatState.CreateCard<SentryWave>(Owner),
        CombatState.CreateCard<SentryWave>(Owner)
    ];
}

public sealed class PackageShapes() : GuardianPackageCard(0)
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<TimeBomb>(IsUpgraded),
        HoverTipFactory.FromCard<SpikerProtocol>(IsUpgraded),
        HoverTipFactory.FromCard<Repulse>(IsUpgraded)
    ];

    protected override IEnumerable<CardModel> CreatePackageCards() =>
    [
        CombatState!.CreateCard<TimeBomb>(Owner),
        CombatState.CreateCard<SpikerProtocol>(Owner),
        CombatState.CreateCard<Repulse>(Owner)
    ];
}

public sealed class PackageSphere() : GuardianPackageCard(2)
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<SphericShield>(IsUpgraded),
        HoverTipFactory.FromCard<FloatingOrbs>(IsUpgraded),
        HoverTipFactory.FromCard<GuardianHarden>(IsUpgraded)
    ];

    protected override IEnumerable<CardModel> CreatePackageCards() =>
    [
        CombatState!.CreateCard<SphericShield>(Owner),
        CombatState.CreateCard<FloatingOrbs>(Owner),
        CombatState.CreateCard<GuardianHarden>(Owner)
    ];
}

public sealed class PackageWalker() : GuardianPackageCard(0)
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<Orbwalk>(IsUpgraded),
        HoverTipFactory.FromCard<WalkerClaw>(IsUpgraded),
        HoverTipFactory.FromCard<Incinerate>(IsUpgraded)
    ];

    protected override IEnumerable<CardModel> CreatePackageCards() =>
    [
        CombatState!.CreateCard<Orbwalk>(Owner),
        CombatState.CreateCard<WalkerClaw>(Owner),
        CombatState.CreateCard<Incinerate>(Owner)
    ];
}

public sealed class PackageDefect() : GuardianPackageCard(1)
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<Reroute>(IsUpgraded),
        HoverTipFactory.FromCard<Preprogram>(IsUpgraded),
        HoverTipFactory.FromCard<TimeCapacitor>(IsUpgraded)
    ];

    protected override IEnumerable<CardModel> CreatePackageCards() =>
    [
        CombatState!.CreateCard<Reroute>(Owner),
        CombatState.CreateCard<Preprogram>(Owner),
        CombatState.CreateCard<TimeCapacitor>(Owner)
    ];
}
