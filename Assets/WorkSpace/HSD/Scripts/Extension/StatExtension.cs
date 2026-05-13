using UnityEngine;

namespace GaeGGUL.Extension
{
    public static class StatExtension
    {
        private static UnitStatPalette _palette;

        private static UnitStatPalette Palette
        {
            get
            {
                if (_palette == null)
                {
                    _palette = Resources.Load<UnitStatPalette>("Data/UnitStatPalette");
                }
                return _palette;
            }
        }

        public static UnitStatVisualInfo GetVisual(this UnitStatType type) => Palette?.GetInfo(type);
    }
}
