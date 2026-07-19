using System.Drawing;
using PetDesktop.Core.Configuration;

namespace PetDesktop.App.Menus;

internal sealed record PetMenuPalette(
    Color Surface,
    Color Hover,
    Color Text,
    Color SecondaryText,
    Color Border,
    Color Track,
    Color Accent,
    Color Knob,
    Color KnobOutline)
{
    public static PetMenuPalette From(AppTheme theme) => theme switch
    {
        AppTheme.Light => new(
            Color.FromArgb(255, 255, 255),
            Color.FromArgb(227, 240, 255),
            Color.FromArgb(36, 36, 38),
            Color.FromArgb(94, 94, 99),
            Color.FromArgb(216, 218, 223),
            Color.FromArgb(232, 233, 237),
            Color.FromArgb(0, 122, 255),
            Color.White,
            Color.FromArgb(165, 165, 165)),
        _ => new(
            Color.FromArgb(44, 44, 46),
            Color.FromArgb(34, 60, 91),
            Color.FromArgb(230, 230, 235),
            Color.FromArgb(178, 178, 184),
            Color.FromArgb(61, 61, 64),
            Color.FromArgb(58, 58, 60),
            Color.FromArgb(10, 132, 255),
            Color.White,
            Color.FromArgb(12, 12, 12)),
    };
}
