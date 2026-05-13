using UnityEngine;

namespace GaeGGUL.Extension
{
    /// <summary>
    /// Tier(Enum)에 대한 시각적 정보를 편리하게 가져오기 위한 확장 클래스입니다.
    /// </summary>
    public static class TierExtension
    {
        private static UnitTierPalette _palette;

        private static UnitTierPalette Palette
        {
            get
            {
                if (_palette == null)
                {
                    // Resources 폴더 내의 경로를 지정합니다.
                    _palette = Resources.Load<UnitTierPalette>("Data/UnitTierPalette");
                    if (_palette == null)
                    {
                        Debug.LogError("[TierExtension] UnitTierPalette를 'Resources/Data/UnitTierPalette'에서 찾을 수 없습니다.");
                    }
                }
                return _palette;
            }
        }

        private static UnitTierUIInfo GetInfo(this Tier tier) => Palette?.GetInfo(tier);

        /// <summary>등급에 맞는 테두리 스프라이트를 반환합니다.</summary>
        public static Sprite GetFrame(this Tier tier) => tier.GetInfo()?.frameSprite;

        /// <summary>등급에 맞는 배경색을 반환합니다.</summary>
        public static Color GetBGColor(this Tier tier) => tier.GetInfo()?.bgColor ?? Color.white;

        /// <summary>등급에 맞는 텍스트 색상을 반환합니다.</summary>
        public static Color GetTextColor(this Tier tier) => tier.GetInfo()?.textColor ?? Color.white;

        /// <summary>등급의 전시용 이름을 반환합니다.</summary>
        public static string GetName(this Tier tier) => tier.GetInfo()?.tierName ?? tier.ToString();
        
        /// <summary>등급 이름을 설정된 색상이 입혀진 Rich Text로 반환합니다.</summary>
        public static string GetColoredName(this Tier tier)
        {
            var info = tier.GetInfo();
            if (info == null) return tier.ToString();
            return info.tierName.ToColor(info.textColor);
        }
    }
}
