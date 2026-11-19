using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace Packmaster.Cards.Hermit;

public sealed class DeadOrAlive()
    : PackmasterCard(0, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
    protected override string PortraitKey => "dead_or_alive";

    protected override bool HasEnergyCostX => true;
    public override bool CanBeGeneratedInCombat => false;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(8m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Fatal),
        new HoverTip(
            new LocString("gameplay_ui", "PACKMASTER-BOUNTY_KEYWORD.title"),
            new LocString("gameplay_ui", "PACKMASTER-BOUNTY_KEYWORD.description"))
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        int hits = ResolveEnergyXValue();
        if (hits <= 0)
            return;

        bool shouldTriggerFatal = cardPlay.Target.Powers.All(p => p.ShouldOwnerDeathTriggerFatal());
        var attack = await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(hits)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);

        if (shouldTriggerFatal
            && attack.Results.SelectMany(r => r).Any(r => r.WasTargetKilled))
        {
            int gold = CombatState?.Encounter?.RoomType switch
            {
                RoomType.Boss => 100,
                RoomType.Elite => 40,
                _ => 15
            };
            await PlayerCmd.GainGold(gold, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
    }
}
