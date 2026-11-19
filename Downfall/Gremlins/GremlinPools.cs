using BaseLib.Abstracts;
using Godot;

namespace Downfall.Gremlins;

public sealed class GremlinCardPool : CustomCardPoolModel
{
    public override string Title => TheGremlins.CharacterId;

    public override string BigEnergyIconPath => "res://Downfall/images/gremlins/charui/big_energy.png";
    public override string TextEnergyIconPath => "res://Downfall/images/gremlins/charui/text_energy.png";

    public override Color ShaderColor => TheGremlins.CharacterColor;
    public override Color DeckEntryCardColor => TheGremlins.CharacterColor;

    public override bool IsColorless => false;
}

public sealed class GremlinRelicPool : CustomRelicPoolModel
{
    public override Color LabOutlineColor => TheGremlins.CharacterColor;

    public override string BigEnergyIconPath => "res://Downfall/images/gremlins/charui/big_energy.png";
    public override string TextEnergyIconPath => "res://Downfall/images/gremlins/charui/text_energy.png";
}

public sealed class GremlinPotionPool : CustomPotionPoolModel
{
    public override Color LabOutlineColor => TheGremlins.CharacterColor;

    public override string BigEnergyIconPath => "res://Downfall/images/gremlins/charui/big_energy.png";
    public override string TextEnergyIconPath => "res://Downfall/images/gremlins/charui/text_energy.png";
}
