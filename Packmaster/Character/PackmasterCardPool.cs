using BaseLib.Abstracts;
using Godot;

namespace Packmaster.Character;

public class PackmasterCardPool : CustomCardPoolModel
{
    public override string Title => ThePackmaster.CharacterId;

    public override string BigEnergyIconPath => "res://Packmaster/images/charui/big_energy.png";
    public override string TextEnergyIconPath => "res://Packmaster/images/charui/text_energy.png";

    public override Color ShaderColor => ThePackmaster.CharacterColor;
    public override Color DeckEntryCardColor => ThePackmaster.CharacterColor;

    public override bool IsColorless => false;
}
