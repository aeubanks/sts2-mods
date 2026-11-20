using BaseLib.Utils;
using Downfall.Core;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace Downfall.Hexaghost.Cards;

[Pool(typeof(TokenCardPool))]
public sealed class HexaAdvanceChoice()
    : DownfallCard(0, CardType.Skill, CardRarity.Token, TargetType.Self, showInCardLibrary: false)
{
    protected override string CharacterKey => TheHexaghost.CharKey;
    public override bool CanBeGeneratedInCombat => false;
}

[Pool(typeof(TokenCardPool))]
public sealed class HexaRetractChoice()
    : DownfallCard(0, CardType.Skill, CardRarity.Token, TargetType.Self, showInCardLibrary: false)
{
    protected override string CharacterKey => TheHexaghost.CharKey;
    public override bool CanBeGeneratedInCombat => false;
}

[Pool(typeof(TokenCardPool))]
public sealed class HexaStayChoice()
    : DownfallCard(0, CardType.Skill, CardRarity.Token, TargetType.Self, showInCardLibrary: false)
{
    protected override string CharacterKey => TheHexaghost.CharKey;
    public override bool CanBeGeneratedInCombat => false;
}
