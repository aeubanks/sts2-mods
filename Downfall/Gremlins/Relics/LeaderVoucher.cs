using Downfall.Gremlins.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace Downfall.Gremlins.Relics;

public sealed class LeaderVoucher : GremlinRelic, IGremlinSacrificeProvider
{
    private int _sacrificedKindIndex = -1;

    [SavedProperty]
    public int SacrificedKindIndex
    {
        get => _sacrificedKindIndex;
        set
        {
            AssertMutable();
            _sacrificedKindIndex = value;
        }
    }

    public GremlinKind? SacrificedGremlin =>
        _sacrificedKindIndex >= 0 && _sacrificedKindIndex < GremlinMob.AllKinds.Length
            ? (GremlinKind)_sacrificedKindIndex
            : null;

    public override RelicRarity Rarity => RelicRarity.Rare;
    public override bool HasUponPickupEffect => true;

    protected override bool IsAllowedInternal(IRunState runState) =>
        runState.Players.Any(p =>
            p.Character is TheGremlins
            && GremlinMob.AllKinds.Count(k => !p.Relics.OfType<IGremlinSacrificeProvider>().Any(r => r.SacrificedGremlin == k)) >= 2);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(1)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            yield return HoverTipFactory.ForEnergy(this);
            if (IsMutable && Owner is not null && SacrificedGremlin is { } kind)
            {
                yield return HoverTipForKind(kind);
            }
        }
    }

    private static IHoverTip HoverTipForKind(GremlinKind kind) => kind switch
    {
        GremlinKind.Mad => HoverTipFactory.FromPower<MadGremlinPower>(),
        GremlinKind.Fat => HoverTipFactory.FromPower<FatGremlinPower>(),
        GremlinKind.Shield => HoverTipFactory.FromPower<ShieldGremlinPower>(),
        GremlinKind.Sneaky => HoverTipFactory.FromPower<SneakyGremlinPower>(),
        GremlinKind.Wizard => HoverTipFactory.FromPower<GremlinWizardPower>(),
        _ => HoverTipFactory.FromPower<MadGremlinPower>()
    };

    public override async Task AfterObtained()
    {
        if (_sacrificedKindIndex < 0)
        {
            var alreadySacrificed = Owner.Relics
                .OfType<IGremlinSacrificeProvider>()
                .Where(r => !ReferenceEquals(r, this) && r.SacrificedGremlin is not null)
                .Select(r => r.SacrificedGremlin!.Value)
                .ToHashSet();

            var available = GremlinMob.AllKinds.Where(k => !alreadySacrificed.Contains(k)).ToList();
            if (available.Count >= 2)
            {
                // Never sacrifice the last living Gremlin.
                var living = available.Where(k => GremlinMob.GetGremlinHp(Owner, k) > 0).ToList();
                var candidates = living.Count > 1
                    ? available
                    : available.Where(k => GremlinMob.GetGremlinHp(Owner, k) == 0).ToList();

                if (candidates.Count > 0)
                {
                    var chosen = candidates[Owner.RunState.Rng.Niche.NextInt(candidates.Count)];
                    SacrificedKindIndex = (int)chosen;
                    await GremlinMob.SacrificeAsync(Owner, chosen, new ThrowingPlayerChoiceContext());
                }
            }
        }
    }

    public override decimal ModifyMaxEnergy(Player player, decimal amount) =>
        player == Owner ? amount + DynamicVars.Energy.BaseValue : amount;
}
