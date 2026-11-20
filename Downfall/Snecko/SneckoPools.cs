using BaseLib.Abstracts;
using Godot;

namespace Downfall.Snecko;

public sealed class SneckoCardPool : CustomCardPoolModel
{
    public override string Title => TheSnecko.CharacterId;

    public override string BigEnergyIconPath => "res://Downfall/images/snecko/charui/big_energy.png";
    public override string TextEnergyIconPath => "res://Downfall/images/snecko/charui/text_energy.png";

    public override Color ShaderColor => TheSnecko.CharacterColor;
    public override Color DeckEntryCardColor => TheSnecko.CharacterColor;

    public override bool IsColorless => false;
}

public sealed class SneckoRelicPool : CustomRelicPoolModel
{
    public override Color LabOutlineColor => TheSnecko.CharacterColor;

    public override string BigEnergyIconPath => "res://Downfall/images/snecko/charui/big_energy.png";
    public override string TextEnergyIconPath => "res://Downfall/images/snecko/charui/text_energy.png";
}

public sealed class SneckoPotionPool : CustomPotionPoolModel
{
    public override Color LabOutlineColor => TheSnecko.CharacterColor;

    public override string BigEnergyIconPath => "res://Downfall/images/snecko/charui/big_energy.png";
    public override string TextEnergyIconPath => "res://Downfall/images/snecko/charui/text_energy.png";
}
