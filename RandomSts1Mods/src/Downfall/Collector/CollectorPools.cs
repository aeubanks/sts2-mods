using BaseLib.Abstracts;
using Godot;

namespace Downfall.Collector;

public sealed class CollectorCardPool : CustomCardPoolModel
{
    public override string Title => TheCollector.CharacterId;

    public override string BigEnergyIconPath => "res://Downfall/images/collector/charui/big_energy.png";
    public override string TextEnergyIconPath => "res://Downfall/images/collector/charui/text_energy.png";

    public override Color ShaderColor => TheCollector.CharacterColor;
    public override Color DeckEntryCardColor => TheCollector.CharacterColor;

    public override bool IsColorless => false;
}

public sealed class CollectorRelicPool : CustomRelicPoolModel
{
    public override Color LabOutlineColor => TheCollector.CharacterColor;

    public override string BigEnergyIconPath => "res://Downfall/images/collector/charui/big_energy.png";
    public override string TextEnergyIconPath => "res://Downfall/images/collector/charui/text_energy.png";
}

public sealed class CollectorPotionPool : CustomPotionPoolModel
{
    public override Color LabOutlineColor => TheCollector.CharacterColor;

    public override string BigEnergyIconPath => "res://Downfall/images/collector/charui/big_energy.png";
    public override string TextEnergyIconPath => "res://Downfall/images/collector/charui/text_energy.png";
}
