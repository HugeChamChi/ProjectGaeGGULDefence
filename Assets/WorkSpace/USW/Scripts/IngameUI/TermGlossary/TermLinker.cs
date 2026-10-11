using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// 카드 설명 문자열에서 용어 별칭을 찾아 TMP 링크(&lt;link="term:Id"&gt;)로 감싼다.
/// 기존 태그와 이미 걸린 링크 안은 건드리지 않고, 같은 자리에서 여러 별칭이 맞으면 가장 긴 것을 쓴다
/// (DebuffInfoPresenter.AddLinks와 같은 규칙). 영상이 있는 용어는 색상으로만 구분한다.
/// </summary>
public sealed class TermLinker
{
    /// <summary>링크 ID 앞붙이. 디버프 링크("debuff:")와 구분한다.</summary>
    public const string LinkPrefix = "term:";
    private static readonly Regex LinkIdPattern = new Regex("<link=\"" + LinkPrefix + "(.+?)\">");

    private readonly TermGlossary _glossary;
    private readonly string _textHex;
    private readonly string _videoHex;

    /// <summary>Creates clickable terms distinguished only by their text/video colors.</summary>
    public TermLinker(TermGlossary glossary, Color textColor, Color videoColor)
    {
        _glossary = glossary;
        _textHex = ColorUtility.ToHtmlStringRGB(textColor);
        _videoHex = ColorUtility.ToHtmlStringRGB(videoColor);
    }

    /// <summary>용어에 링크를 건 설명을 돌려준다. 사전이 없으면 원문 그대로.</summary>
    public string AddLinks(string text)
        => AddLinks(text, null);

    /// <summary>Limits automatic links to the terms authored for this card.</summary>
    public string AddLinks(string text, IReadOnlyList<string> allowedIds)
    {
        if (string.IsNullOrEmpty(text) || _glossary == null) return text;
        var output = new StringBuilder(text.Length + 64);
        bool insideLink = false;
        for (int i = 0; i < text.Length;)
        {
            if (text[i] == '<')
            {
                int end = text.IndexOf('>', i);
                if (end < 0) { output.Append(text, i, text.Length - i); break; }
                string tag = text.Substring(i, end - i + 1);
                if (tag.StartsWith("<link", StringComparison.OrdinalIgnoreCase)) insideLink = true;
                else if (tag.StartsWith("</link", StringComparison.OrdinalIgnoreCase)) insideLink = false;
                output.Append(tag);
                i = end + 1;
                continue;
            }

            TermEntry hit = null;
            string match = null;
            if (!insideLink)
            {
                foreach (var entry in _glossary.Entries)
                {
                    if (entry?.Aliases == null) continue;
                    if (allowedIds != null)
                    {
                        bool allowed = false;
                        foreach (var id in allowedIds) if (id == entry.Id) { allowed = true; break; }
                        if (!allowed) continue;
                    }
                    foreach (string alias in entry.Aliases)
                    {
                        if (string.IsNullOrEmpty(alias) || (match != null && alias.Length <= match.Length)) continue;
                        if (i + alias.Length <= text.Length && string.CompareOrdinal(text, i, alias, 0, alias.Length) == 0)
                        { match = alias; hit = entry; }
                    }
                }
            }
            if (hit == null) { output.Append(text[i++]); continue; }

            // <nobr>: "해킹 / 스택"처럼 용어가 두 줄로 갈리면 읽기 어렵고, 두 줄 사이를 누르면 용어가 아닌 카드가 눌린다.
            bool video = hit.Clip != null;
            output.Append("<link=\"").Append(LinkPrefix).Append(hit.Id).Append("\"><nobr><color=#")
                  .Append(video ? _videoHex : _textHex).Append(">");
            output.Append(match).Append("</color></nobr></link>");
            i += match.Length;
        }
        return output.ToString();
    }

    /// <summary>링크가 걸린 문자열에 들어 있는 용어 ID들 (순서대로, 중복 없이). 시트의 "이 카드의 다른 용어"용.</summary>
    public static List<string> LinkedIds(string linkedText)
    {
        var ids = new List<string>();
        if (string.IsNullOrEmpty(linkedText)) return ids;
        foreach (Match m in LinkIdPattern.Matches(linkedText))
            if (!ids.Contains(m.Groups[1].Value)) ids.Add(m.Groups[1].Value);
        return ids;
    }
}
