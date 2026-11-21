using BaseLib.Abstracts;
using Godot;

namespace Downfall.Champ;

public sealed class ChampCardPool : CustomCardPoolModel
{
    public override string Title => TheChamp.CharacterId;

    public override string BigEnergyIconPath => "res://Downfall/images/champ/charui/big_energy.png";
    public override string TextEnergyIconPath => "res://Downfall/images/champ/charui/text_energy.png";

    public override Color ShaderColor => TheChamp.CharacterColor;
    public override Color DeckEntryCardColor => TheChamp.CharacterColor;

    public override bool IsColorless => false;
}

public sealed class ChampRelicPool : CustomRelicPoolModel
{
    public override Color LabOutlineColor => TheChamp.CharacterColor;

    public override string BigEnergyIconPath => "res://Downfall/images/champ/charui/big_energy.png";
    public override string TextEnergyIconPath => "res://Downfall/images/champ/charui/text_energy.png";
}

public sealed class ChampPotionPool : CustomPotionPoolModel
{
    public override Color LabOutlineColor => TheChamp.CharacterColor;

    public override string BigEnergyIconPath => "res://Downfall/images/champ/charui/big_energy.png";
    public override string TextEnergyIconPath => "res://Downfall/images/champ/charui/text_energy.png";
}
