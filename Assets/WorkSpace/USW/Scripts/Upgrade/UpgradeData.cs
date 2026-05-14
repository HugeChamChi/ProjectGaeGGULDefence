/// <summary>
/// 구글 시트에서 파싱된 강화 비용 데이터 (gid=297223937)
/// </summary>
public class JobUpgradeCostRow
{
    public string UpgradeType;    // "Job_Upgrade" | "Currency"
    public string UpgradeTarget;  // "Frog" | "Frog_Gunner" | "Frog_Ninja" | "Frog_Magician" | "All"
    public int    InitialCost;
    public float  CostRate;       // 배율 증가 (Job_Upgrade: ×2.0)
    public int    CostIncrease;   // 고정 증가 (Currency: +50)
}

/// <summary>
/// 구글 시트에서 파싱된 캐릭터 강화 레벨별 스탯 (gid=1454519483)
/// </summary>
public class CharacterStatRow
{
    public int    CharacterId;
    public string CharacterType;  // "Frog" | "Frog_Gunner" | "Frog_Ninja" | "Frog_Magician"
    public int    Level;          // 강화 레벨 1~10
    public float  Atk;
    public float  AttackSpeed;    // 공격 간격(초) — 낮을수록 빠름
}
