using BaseLib.Abstracts;
using Godot;

namespace Downfall.Hermit;

public sealed class HermitCardPool : CustomCardPoolModel
{
    public override string Title => TheHermit.CharacterId;

    public override string BigEnergyIconPath => "res://Downfall/images/hermit/charui/big_energy.png";
    public override string TextEnergyIconPath => "res://Downfall/images/hermit/charui/text_energy.png";

    public override Color ShaderColor => TheHermit.CharacterColor;
    public override Color DeckEntryCardColor => TheHermit.CharacterColor;

    public override bool IsColorless => false;
}
