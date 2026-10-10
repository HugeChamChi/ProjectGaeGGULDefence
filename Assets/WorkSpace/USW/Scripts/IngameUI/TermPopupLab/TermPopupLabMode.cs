/// <summary>용어 팝업 실험실 비교 모드.</summary>
public enum TermPopupLabMode
{
    /// <summary>A: 단어 옆 말풍선. 화면 중앙보다 위 단어는 아래로, 아래 단어는 위로 뜬다. 전체 설명 표시.</summary>
    Bubble = 0,
    /// <summary>B: 아래에서 올라오는 시트. 가려지는 카드는 위로 밀어 올린다.</summary>
    Sheet = 1,
    /// <summary>추천안: 말풍선에 요약만, "자세히"를 누르면 시트로 전체 설명.</summary>
    BubbleThenSheet = 2,
}
