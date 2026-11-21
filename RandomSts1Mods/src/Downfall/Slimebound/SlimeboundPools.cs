using BaseLib.Abstracts;
using Godot;

namespace Downfall.Slimebound;

public sealed class SlimeboundCardPool : CustomCardPoolModel
{
    public override string Title => TheSlimeBoss.CharacterId;

    public override string BigEnergyIconPath => "res://Downfall/images/slimebound/charui/big_energy.png";
    public override string TextEnergyIconPath => "res://Downfall/images/slimebound/charui/text_energy.png";

    public override Color ShaderColor => TheSlimeBoss.CharacterColor;
    public override Color DeckEntryCardColor => TheSlimeBoss.CharacterColor;

    public override bool IsColorless => false;
}

public sealed class SlimeboundRelicPool : CustomRelicPoolModel
{
    public override Color LabOutlineColor => TheSlimeBoss.CharacterColor;

    public override string BigEnergyIconPath => "res://Downfall/images/slimebound/charui/big_energy.png";
    public override string TextEnergyIconPath => "res://Downfall/images/slimebound/charui/text_energy.png";
}

public sealed class SlimeboundPotionPool : CustomPotionPoolModel
{
    public override Color LabOutlineColor => TheSlimeBoss.CharacterColor;

    public override string BigEnergyIconPath => "res://Downfall/images/slimebound/charui/big_energy.png";
    public override string TextEnergyIconPath => "res://Downfall/images/slimebound/charui/text_energy.png";
}
