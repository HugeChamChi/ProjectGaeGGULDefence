/// <summary>결과 화면 톤. 실험실 FxLab_Result에서 비교한다 (2026-10-01).</summary>
public enum ResultScreenTone
{
    /// <summary>A. 항상 담백한 기록 화면. 신기록이면 문구 색만 바뀐다.</summary>
    Plain = 0,

    /// <summary>B. 항상 축하 (기절한 몬스터 배너 + 노란 배경 + 색종이).</summary>
    Celebrate = 1,

    /// <summary>C. 평소엔 담백, 신기록일 때만 도장·노란 배경·색종이.</summary>
    RecordOnly = 2,
}
