using System;
using System.Text;
using UnityEngine;

/// <summary>Pure template rendering. Only explicit tokens are interpreted; TMP tags are preserved.</summary>
public static class DescriptionFormatter
{
    public static string Format(DescriptionSnapshot snapshot, bool detailed, IDescriptionTermResolver resolver, bool interactive)
    {
        string text = detailed || string.IsNullOrWhiteSpace(snapshot.Simple) ? snapshot.Detailed : snapshot.Simple;
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
                output.Append(tag); i = end + 1; continue;
            }
            if (text[i] != '{') { output.Append(text[i++]); continue; }
            int close = text.IndexOf('}', i + 1);
            if (close < 0) { output.Append(text, i, text.Length - i); break; }
            string token = text.Substring(i + 1, close - i - 1);
            if (token.StartsWith("term:", StringComparison.Ordinal))
            {
                string id = token.Substring(5);
                if (resolver == null || !resolver.TryResolve(id, out var term)) output.Append('[').Append(id).Append(']');
                else
                {
                    string name = (term.DisplayName ?? id).Replace("<", "\uFF1C").Replace(">", "\uFF1E");
                    bool link = interactive && !insideLink && !string.IsNullOrWhiteSpace(term.Body);
                    if (link) output.Append("<link=\"").Append(id).Append("\"><color=#").Append(ColorUtility.ToHtmlStringRGB(term.Color)).Append("><u>");
                    output.Append('[').Append(name).Append(']');
                    if (link) output.Append("</u></color></link>");
                }
            }
            else if (snapshot.Values.TryGetValue(token, out string value)) output.Append(value);
            else output.Append(text, i, close - i + 1);
            i = close + 1;
        }
        return output.ToString();
    }
}
