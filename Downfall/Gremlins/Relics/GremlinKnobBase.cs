using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace Downfall.Gremlins.Relics;

public abstract class GremlinKnobBase : GremlinRelic
{
    private string _mobStateEncoded = string.Empty;

    [SavedProperty]
    public string MobStateEncoded
    {
        get => _mobStateEncoded;
        set
        {
            AssertMutable();
            _mobStateEncoded = value ?? string.Empty;
        }
    }

    protected IEnumerable<IHoverTip> BaseKnobHoverTips
    {
        get
        {
            yield return HoverTipFactory.ForEnergy(this);
            yield return GremlinCard.SwapHoverTip;
            if (IsMutable && Owner is not null && Owner.Character is TheGremlins)
            {
                yield return GremlinMob.BuildMobStatusHoverTip(Owner);
            }
        }
    }

    public override bool ShouldDie(Creature creature)
    {
        if (creature != Owner.Creature)
            return true;

        return !GremlinMob.ShouldPreventDeath(Owner);
    }

    public override async Task AfterPreventingDeath(Creature creature)
    {
        if (creature != Owner.Creature)
            return;

        await GremlinMob.HandleActiveGremlinDeathAsync(Owner);
    }

    public override Task AfterRemoved()
    {
        if (Owner.Character is TheGremlins)
        {
            GremlinMob.CacheEncodedStateIfMissing(Owner, MobStateEncoded);
        }
        return Task.CompletedTask;
    }

    public override async Task BeforeCombatStart()
    {
        if (Owner.Creature.CombatState is { } combatState && Owner.Character is TheGremlins)
        {
            await GremlinMob.EnsureInitializedAsync(combatState, Owner, new ThrowingPlayerChoiceContext());
        }
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        if (Owner.Character is TheGremlins)
        {
            GremlinMob.OnCombatEnd(Owner);
        }
        return Task.CompletedTask;
    }

    public override Task AfterRestSiteHeal(Player player, bool isMimicked)
    {
        if (player != Owner || Owner.Character is not TheGremlins)
            return Task.CompletedTask;

        int healAmount = Math.Max(1, (int)HealRestSiteOption.GetHealAmount(player));
        GremlinMob.SyncFrontHpFromCreature(player);
        GremlinMob.HealStandbyGremlins(player, healAmount);

        if (GremlinMob.CanResurrect(player))
        {
            int rezHp = Math.Max(1, player.Creature.MaxHp / 2);
            GremlinMob.ResurrectRandom(player, rezHp, preferCombatRng: false);
        }

        Flash();
        return Task.CompletedTask;
    }

    public override IReadOnlyList<LocString> ModifyExtraRestSiteHealText(
        Player player,
        IReadOnlyList<LocString> currentExtraText)
    {
        if (player != Owner
            || Owner.Character is not TheGremlins
            || !LocalContext.IsMe(Owner)
            || AdditionalRestSiteHealText is not { } extraText)
        {
            return currentExtraText;
        }

        var result = new LocString[currentExtraText.Count + 1];
        for (int i = 0; i < currentExtraText.Count; i++)
        {
            result[i] = currentExtraText[i];
        }
        result[currentExtraText.Count] = extraText;
        return result;
    }

    public override async Task AfterActEntered()
    {
        if (Owner.Character is not TheGremlins)
            return;
        if (Owner.RunState.CurrentActIndex <= 0 && Owner.RunState.TotalFloor <= 0)
            return;

        await GremlinMob.HealAllToFullAndResurrectAllAsync(Owner);
    }
}
