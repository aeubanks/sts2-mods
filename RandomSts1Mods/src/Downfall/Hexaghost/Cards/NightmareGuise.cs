using BaseLib.Utils;
using Downfall.Core;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;

namespace Downfall.Hexaghost.Cards;

/// <summary>
/// Ethereal. Afterlife. Gain 4 (5) Block. Add a Shadow Guise (Shadow Guise+) to your Hand.
/// Afterlife: Add a Shadow Guise (Shadow Guise+) to your Hand.
/// </summary>
public sealed class NightmareGuise() : HexaCard(1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    public override bool GainsBlock => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal];
    public override bool HasAfterlife => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(4m, ValueProp.Move)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        AfterlifeHoverTip,
        HoverTipFactory.FromCard<ShadowGuise>(IsUpgraded)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await AddShadowGuiseAsync();
    }

    protected override async Task OnAfterlifeAsync(PlayerChoiceContext choiceContext, bool causedByEthereal)
    {
        await AddShadowGuiseAsync();
    }

    private async Task AddShadowGuiseAsync()
    {
        if (Owner.Creature.CombatState is not { } combatState
            || !Owner.Creature.IsAlive
            || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        var token = combatState.CreateCard<ShadowGuise>(Owner);
        if (IsUpgraded)
            CardCmd.Upgrade(token);
        await CardPileCmd.AddGeneratedCardToCombat(token, PileType.Hand, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(1m);
    }
}

[Pool(typeof(TokenCardPool))]
public sealed class ShadowGuise()
    : DownfallCard(0, CardType.Skill, CardRarity.Token, TargetType.Self, showInCardLibrary: false)
{
    protected override string CharacterKey => TheHexaghost.CharKey;
    public override bool CanBeGeneratedInCombat => false;
    public override bool GainsBlock => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain, CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(4m, ValueProp.Move)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
    }
}
