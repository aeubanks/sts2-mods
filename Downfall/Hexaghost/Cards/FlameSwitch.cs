using BaseLib.Utils;
using Downfall.Core;
using Downfall.Hexaghost.Orbs;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace Downfall.Hexaghost.Cards;

/// <summary>
/// Bad Omen (FlameSwitch): Choose a Ghostflame type to replace the active Ghostflame with.
/// Exhaust. Upgraded: adds Retain.
/// </summary>
public sealed class FlameSwitch() : HexaCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        GhostflameHoverTip
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.Creature.CombatState is not { } combatState
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        var flames = HexaMechanics.GetGhostflames(Owner);
        if (flames.Count == 0)
            return;

        CardModel[] choices =
        [
            combatState.CreateCard<HexaSearingChoice>(Owner),
            combatState.CreateCard<HexaCrushingChoice>(Owner),
            combatState.CreateCard<HexaBolsteringChoice>(Owner)
        ];

        var chosen = await CardSelectCmd.FromChooseACardScreen(choiceContext, choices, Owner, canSkip: true);
        int activeIdx = HexaMechanics.GetActiveSlotIndex(Owner);
        GhostflameOrb? replacement = chosen switch
        {
            HexaSearingChoice => (GhostflameOrb)ModelDb.Orb<SearingGhostflameOrb>().ToMutable(),
            HexaCrushingChoice => (GhostflameOrb)ModelDb.Orb<CrushingGhostflameOrb>().ToMutable(),
            HexaBolsteringChoice => (GhostflameOrb)ModelDb.Orb<BolsteringGhostflameOrb>().ToMutable(),
            _ => null
        };

        if (replacement is not null)
            HexaMechanics.ReplaceGhostflame(Owner, activeIdx, replacement);
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}

[Pool(typeof(TokenCardPool))]
public sealed class HexaSearingChoice()
    : DownfallCard(0, CardType.Skill, CardRarity.Token, TargetType.Self, showInCardLibrary: false)
{
    protected override string CharacterKey => TheHexaghost.CharKey;
    public override bool CanBeGeneratedInCombat => false;
}

[Pool(typeof(TokenCardPool))]
public sealed class HexaCrushingChoice()
    : DownfallCard(0, CardType.Skill, CardRarity.Token, TargetType.Self, showInCardLibrary: false)
{
    protected override string CharacterKey => TheHexaghost.CharKey;
    public override bool CanBeGeneratedInCombat => false;
}

[Pool(typeof(TokenCardPool))]
public sealed class HexaBolsteringChoice()
    : DownfallCard(0, CardType.Skill, CardRarity.Token, TargetType.Self, showInCardLibrary: false)
{
    protected override string CharacterKey => TheHexaghost.CharKey;
    public override bool CanBeGeneratedInCombat => false;
}
