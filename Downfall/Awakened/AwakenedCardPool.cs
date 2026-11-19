using BaseLib.Abstracts;
using Godot;

namespace Downfall.Awakened;

public sealed class AwakenedCardPool : CustomCardPoolModel
{
    public override string Title => TheAwakenedOne.CharacterId;

    public override string BigEnergyIconPath => "res://Downfall/images/awakened/charui/big_energy.png";
    public override string TextEnergyIconPath => "res://Downfall/images/awakened/charui/text_energy.png";

    public override Color ShaderColor => TheAwakenedOne.CharacterColor;
    public override Color DeckEntryCardColor => TheAwakenedOne.CharacterColor;

    public override bool IsColorless => false;
}
