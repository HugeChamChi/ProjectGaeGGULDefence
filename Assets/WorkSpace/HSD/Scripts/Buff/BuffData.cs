using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BuffData", menuName = "Game/BuffData")]
public class BuffData : ScriptableObject, ILoadableAsset
{
    [Header("기본 정보")]
    public int buffId;
    public string buffName;
    [TextArea] public string description;

    [Header("Addressables")]
    public string iconAddress;
    [HideInInspector] public Sprite icon;

    public bool IsLoaded => icon != null;

    [Header("지속시간 (0 이하 = 영구)")]
    public float duration = 0f;

    [Header("스택 정책 / 효과 (SelectableReference)")]
    [SerializeReference, SelectableReference]
    public IBuffStackPolicy stackPolicy = new InfiniteStackPolicy();
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
