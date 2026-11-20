using BaseLib.Abstracts;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;

namespace Downfall.Core;

public abstract class DownfallOrb : CustomOrbModel
{
    protected abstract string CharacterKey { get; }
    protected virtual string IconKey => StringHelper.Slugify(GetType().Name).ToLowerInvariant();

    public override bool IncludeInRandomPool => false;
    public override string CustomIconPath => $"res://Downfall/images/{CharacterKey}/orbs/{IconKey}.png";

    /// <summary>
    /// Whether the secondary/evoke counter label should be visible during normal (non-evoking) display.
    /// Dual-stat Slimes (e.g. Leeching, Mire, Insulting, Time, Spiky, Ghostflame) show both Passive and Evoke labels.
    /// </summary>
    public virtual bool ShowSecondaryLabel => false;

    /// <summary>
    /// Text shown in the Evoke label when hovering a card that evokes this orb.
    /// </summary>
    public virtual string EvokePreviewText => EvokeVal.ToString("0");

    /// <summary>
    /// Tint applied to the orb sprite (e.g. dimmed when Bronze Slime is stunned/recharging).
    /// </summary>
    public virtual Color SpriteTint => Colors.White;

    public override Node2D CreateCustomSprite()
    {
        var tex = PreloadManager.Cache.GetCompressedTexture2D(CustomIconPath);
        return new Sprite2D
        {
            Texture = tex,
            Centered = true
        };
    }
}
