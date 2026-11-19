using BaseLib.Utils;
using Downfall.Core;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;

namespace Downfall.Gremlins;

[Pool(typeof(GremlinCardPool))]
public abstract class GremlinCard(
    int cost,
    CardType type,
    CardRarity rarity,
    TargetType target,
    bool showInCardLibrary = true)
    : DownfallCard(cost, type, rarity, target, showInCardLibrary)
{
    protected override string CharacterKey => TheGremlins.CharKey;

    public virtual bool ConsumesWiz => true;
    public virtual bool ScalesBlockWithWiz => false;

    internal static IHoverTip SwapHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-SWAP_KEYWORD.title"),
        new LocString("gameplay_ui", "DOWNFALL-SWAP_KEYWORD.description"));

    internal static IHoverTip TempHpHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-TEMP_HP_KEYWORD.title"),
        new LocString("gameplay_ui", "DOWNFALL-TEMP_HP_KEYWORD.description"));

    internal static IHoverTip EchoHoverTip => new HoverTip(
        new LocString("gameplay_ui", "DOWNFALL-ECHO_KEYWORD.title"),
        new LocString("gameplay_ui", "DOWNFALL-ECHO_KEYWORD.description"));

    public override async Task BeforeCombatStart()
    {
        await base.BeforeCombatStart();
        if (CombatState is not null && Owner is not null && Owner.Character is TheGremlins)
        {
            await GremlinMob.EnsureInitializedAsync(CombatState, Owner, new ThrowingPlayerChoiceContext());
        }
    }
}
