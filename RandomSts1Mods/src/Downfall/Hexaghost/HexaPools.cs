using BaseLib.Abstracts;
using Godot;

namespace Downfall.Hexaghost;

public sealed class HexaCardPool : CustomCardPoolModel
{
    public override string Title => TheHexaghost.CharacterId;

    public override string BigEnergyIconPath => "res://Downfall/images/hexaghost/charui/big_energy.png";
    public override string TextEnergyIconPath => "res://Downfall/images/hexaghost/charui/text_energy.png";

    public override Color ShaderColor => TheHexaghost.CharacterColor;
    public override Color DeckEntryCardColor => TheHexaghost.CharacterColor;

    public override bool IsColorless => false;
}

public sealed class HexaRelicPool : CustomRelicPoolModel
{
    public override Color LabOutlineColor => TheHexaghost.CharacterColor;

    public override string BigEnergyIconPath => "res://Downfall/images/hexaghost/charui/big_energy.png";
    public override string TextEnergyIconPath => "res://Downfall/images/hexaghost/charui/text_energy.png";
}

public sealed class HexaPotionPool : CustomPotionPoolModel
{
    public override Color LabOutlineColor => TheHexaghost.CharacterColor;

    public override string BigEnergyIconPath => "res://Downfall/images/hexaghost/charui/big_energy.png";
    public override string TextEnergyIconPath => "res://Downfall/images/hexaghost/charui/text_energy.png";
}
