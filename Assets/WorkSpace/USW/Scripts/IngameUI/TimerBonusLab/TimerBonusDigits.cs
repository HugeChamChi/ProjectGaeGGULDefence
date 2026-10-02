using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 타이머 숫자 "00.00". 자릿수마다 마스크된 릴(위·아래 TMP 2장)이라 평소 표시와 슬롯 회전을 같은 구조로 그린다.
/// 기울임(skew)은 UGUI에 변환이 없어서 TMP 정점을 직접 민다 — 색·글자를 바꾼 뒤 마지막에 SetSkew를 호출해야 한다.
/// </summary>
public sealed class TimerBonusDigits
{
    private const int DigitCount = 4;
    private const float CellWidthEm = 0.56f;
    private const float CellHeightEm = 1.1f;
    private const float DotWidthEm = 0.26f;
    private const float MaskSoftness = 0.16f;
    private const float BlurStretch = 0.25f;
    private const float BlurFade = 0.35f;
    private static readonly int[] CharIndex = { 0, 1, 3, 4 };
    private static readonly string[] DigitText = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };

    private sealed class Reel
    {
        public RectTransform Cell;
        public TextMeshProUGUI Upper;
        public TextMeshProUGUI Lower;
        public float Fade = 1f;
    }

    private readonly Reel[] _reels = new Reel[DigitCount];
    private readonly TextMeshProUGUI _dot;
    private readonly TextMeshProUGUI[] _texts;
    private bool _skewed;

    /// <summary>숫자 묶음 루트 (가운데 기준).</summary>
    public RectTransform Root { get; }

    /// <summary>전체 크기.</summary>
    public Vector2 Size { get; }

    /// <summary>자릿수 칸 높이.</summary>
    public float CellHeight { get; }

    /// <summary>parent 아래에 숫자 칸을 만든다.</summary>
    public TimerBonusDigits(RectTransform parent, string name, TMP_FontAsset font, float fontSize)
    {
        float cellWidth = fontSize * CellWidthEm;
        float dotWidth = fontSize * DotWidthEm;
        CellHeight = fontSize * CellHeightEm;
        Size = new Vector2(cellWidth * DigitCount + dotWidth, CellHeight);
        Root = TimerBonusLab.NewRect(name, parent, Size);
        _texts = new TextMeshProUGUI[DigitCount * 2 + 1];

        float x = -Size.x * 0.5f;
        int t = 0;
        for (int i = 0; i < DigitCount + 1; i++)
        {
            if (i == 2)
            {
                var dotRt = TimerBonusLab.NewRect("Dot", Root, new Vector2(dotWidth, CellHeight));
                dotRt.anchoredPosition = new Vector2(x + dotWidth * 0.5f, 0f);
                _dot = TimerBonusLab.NewText("Dot", dotRt, font, fontSize, Color.white);
                _dot.text = ".";
                _texts[t++] = _dot;
                x += dotWidth;
                continue;
            }
            var cell = TimerBonusLab.NewRect("Digit" + i, Root, new Vector2(cellWidth, CellHeight));
            cell.anchoredPosition = new Vector2(x + cellWidth * 0.5f, 0f);
            // 세로만 자른다: 가로는 칸 폭만큼 넓혀 기울임(skew)으로 삐져나온 글자 윗부분이 잘리지 않게.
            var mask = cell.gameObject.AddComponent<RectMask2D>();
            mask.softness = new Vector2Int(0, Mathf.RoundToInt(CellHeight * MaskSoftness));
            mask.padding = new Vector4(-cellWidth, 0f, -cellWidth, 0f);
            var reel = new Reel
            {
                Cell = cell,
                Upper = TimerBonusLab.NewText("Upper", cell, font, fontSize, Color.white),
                Lower = TimerBonusLab.NewText("Lower", cell, font, fontSize, Color.white),
            };
            reel.Upper.rectTransform.sizeDelta = reel.Lower.rectTransform.sizeDelta = new Vector2(cellWidth * 1.4f, CellHeight);
            _reels[i < 2 ? i : i - 1] = reel;
            _texts[t++] = reel.Upper;
            _texts[t++] = reel.Lower;
            x += cellWidth;
        }
        SetValue(0d);
    }

    /// <summary>"00.00" 형식 (음수는 0).</summary>
    public static string Format(double seconds) => System.Math.Max(0d, seconds).ToString("00.00", CultureInfo.InvariantCulture);

    /// <summary>릴 번호(0~3)가 가리키는 Format 문자열 위치.</summary>
    public static int CharIndexOf(int reel) => CharIndex[reel];

    /// <summary>릴 칸 중심 (Root 기준).</summary>
    public Vector2 CellPosition(int reel) => _reels[reel].Cell.anchoredPosition;

    /// <summary>모든 자릿수를 값으로 맞춘다 (회전 없음).</summary>
    public void SetValue(double seconds)
    {
        string s = Format(seconds);
        for (int j = 0; j < DigitCount; j++) SetReel(j, s[CharIndex[j]] - '0', 0f);
    }

    /// <summary>
    /// 릴 위치. 정수 = 그 숫자가 칸 가운데, 소수부만큼 다음 숫자가 아래에서 올라온다.
    /// blur(0~1)는 회전 속도감 — 세로로 늘이고 살짝 흐리게.
    /// </summary>
    public void SetReel(int reel, float position, float blur)
    {
        var r = _reels[reel];
        float p = Mathf.Repeat(position, 10f);
        int d0 = Mathf.Min(9, Mathf.FloorToInt(p));
        float frac = p - d0;
        SetText(r.Upper, DigitText[d0]);
        r.Upper.rectTransform.anchoredPosition = new Vector2(0f, frac * CellHeight);
        bool showLower = frac > 0.001f;
        r.Lower.enabled = showLower;
        if (showLower)
        {
            SetText(r.Lower, DigitText[(d0 + 1) % 10]);
            r.Lower.rectTransform.anchoredPosition = new Vector2(0f, (frac - 1f) * CellHeight);
        }
        float b = Mathf.Clamp01(blur);
        var scale = new Vector3(1f, 1f + BlurStretch * b, 1f);
        r.Upper.rectTransform.localScale = r.Lower.rectTransform.localScale = scale;
        r.Fade = 1f - BlurFade * b;
    }

    /// <summary>글자색 (릴 흐림은 알파에 곱해진다).</summary>
    public void SetColor(Color c)
    {
        _dot.color = c;
        foreach (var r in _reels)
        {
            var rc = new Color(c.r, c.g, c.b, c.a * r.Fade);
            r.Upper.color = rc;
            r.Lower.color = rc;
        }
    }

    /// <summary>기울임 (도, 양수 = 윗부분이 오른쪽). 글자·색을 바꾼 뒤 마지막에 호출.</summary>
    public void SetSkew(float degrees)
    {
        bool skew = Mathf.Abs(degrees) > 0.01f;
        if (!skew && !_skewed) return;
        float k = Mathf.Tan(degrees * Mathf.Deg2Rad);
        foreach (var t in _texts)
        {
            if (!t.enabled) continue;
            t.ForceMeshUpdate();
            if (!skew) continue;
            // 모든 칸이 Root 가운데 높이에 있으므로, 칸 안 세로 오프셋만 더하면 묶음 전체가 한 번에 기울어진다.
            float oy = t.rectTransform.anchoredPosition.y;
            var info = t.textInfo;
            for (int m = 0; m < info.meshInfo.Length; m++)
            {
                var verts = info.meshInfo[m].vertices;
                if (verts == null) continue;
                for (int v = 0; v < verts.Length; v++) verts[v].x += (verts[v].y + oy) * k;
            }
            t.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
        }
        _skewed = skew;
    }

    private static void SetText(TMP_Text t, string s)
    {
        if (t.text != s) t.text = s;
    }
}
