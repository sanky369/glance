using UnityEngine;

namespace Modules
{
    public static class ColorExtensionMethods
    {
        /// <summary>
        /// Returns a new Color that has the same RGB values but a different alpha.
        /// </summary>
        /// <param name="color">The original color to tweak.</param>
        /// <param name="alpha">The alpha value to put in place of the old one.</param>
        /// <returns>A new Color, with the same RGB but new alpha.</returns>
        public static Color ChangeAlpha(this Color color, float alpha) => new(color.r, color.g, color.b, alpha);
        
        /// <summary>
        /// Returns a new Color that has the RGB values of the new, desired one; but the alpha of the old one.
        /// </summary>
        /// <param name="color">The color whose RGB needs to change.</param>
        /// <param name="newColor">The desired new RGB values.</param>
        /// <returns>A new Color, with the new RGB and the old alpha.</returns>
        public static Color ChangeIntoColorKeepAlpha(this Color color, Color newColor) => new(newColor.r, newColor.g, newColor.b, color.a);
    }
}