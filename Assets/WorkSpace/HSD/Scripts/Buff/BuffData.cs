using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BuffData", menuName = "Game/BuffData")]
public class BuffData : ScriptableObject, ILoadableAsset
{
    [ColorFoldoutGroup("기본 정보", "#8A8F98")] public int buffId;
    [ColorFoldoutGroup("기본 정보", "#8A8F98")] public string buffName;
    [ColorFoldoutGroup("기본 정보", "#8A8F98"), TextArea] public string description;

    [ColorFoldoutGroup("Addressables", "#14B8A6")] public string iconAddress;
    [HideInInspector] public Sprite icon;

    public bool IsLoaded => icon != null;

    [ColorFoldoutGroup("지속시간 (0 이하 = 영구)", "#F59E0B")]
    public float duration = 0f;

    [ColorFoldoutGroup("스택 정책 / 효과 (SelectableReference)", "#4C8BF5")]
    [SerializeReference, SelectableReference]
    public IBuffStackPolicy stackPolicy = new InfiniteStackPolicy();
    [ColorFoldoutGroup("스택 정책 / 효과 (SelectableReference)", "#4C8BF5")]
    [SerializeReference, SelectableReference]
    public List<IBuffEffect> effects = new List<IBuffEffect>();

    public async Cysharp.Threading.Tasks.UniTask LoadAssetsAsync()
    {
        if (!string.IsNullOrEmpty(iconAddress) && icon == null)
            icon = await RM.LoadAsync<Sprite>(iconAddress);
    }

    public void UnloadAssets()
    {
        if (icon != null)
        {
            RM.Unload(icon);
            icon = null;
        }
    }
}
