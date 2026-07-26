using UnityEditorInternal;

/// <summary>UnitData 인스펙터의 등급 탭 선택 상태(에디터 프로세스 전역 공유).</summary>
public static class TierTabState
{
    private static Tier _selected = Tier.Normal;

    public static Tier Selected
    {
        get => _selected;
        set
        {
            if (_selected == value) return;
            _selected = value;
            InternalEditorUtility.RepaintAllViews();
        }
    }
}
