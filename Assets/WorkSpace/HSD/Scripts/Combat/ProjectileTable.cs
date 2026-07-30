using System.Collections.Generic;
using Cysharp.Threading.Tasks;

/// <summary>
/// ProjectileData.id → ProjectileData 조회 테이블. ItemTable/CharacterTable과 동일한 패턴으로,
/// "ProjectileData" 라벨의 Addressable 에셋을 전부 읽어 id로 인덱싱한다.
/// 스킬 액션 등은 ProjectileData를 직접 참조하지 않고 id로 Table.Projectile.Get(id)를 호출해 조회하므로,
/// 실제 데이터는 언제든 교체할 수 있다. id 0(또는 미등록 id)은 "지정 없음"으로 null을 반환한다.
/// </summary>
public class ProjectileTable
{
    private readonly Dictionary<int, ProjectileData> _dict = new();

    const string LABEL = "ProjectileData";

    public async UniTask InitializeAsync()
    {
        var list = await RM.LoadAllAsync<ProjectileData>(LABEL);
        foreach (var data in list)
            _dict.TryAdd(data.id, data);
    }

    public ProjectileData Get(int id)
        => id != 0 && _dict.TryGetValue(id, out var data) ? data : null;
}
