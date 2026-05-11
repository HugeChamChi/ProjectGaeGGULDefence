using UnityEngine;

namespace GaeGGUL.Extension
{
    public static class StringExtension
    {
        /// <summary>
        /// 문자열에 컬러 태그를 입힙니다.
        /// </summary>
        public static string ToColor(this string text, Color color)
        {
            return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";
        }

        public static string ToColor(this string text, string hexColor)
        {
            return $"<color={hexColor}>{text}</color>";
        }

        public static string ToBold(this string text)
        {
            return $"<b>{text}</b>";
        }
    }
}
