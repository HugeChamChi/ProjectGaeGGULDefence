using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class UI_BossIcon : MonoBehaviour
{
    [Inject] private BossManager _bossManager;

    [SerializeField] private Image _imgBossIcon;

    private void Start()
    {
        if (_bossManager == null) return;
        _bossManager.OnBossEntryed += ChangeIcon;
        ChangeIcon(null, _bossManager.PrevBossEntry);
    }

    private void OnDestroy()
    {
        if ((_bossManager != null))
        {
            _bossManager.OnBossEntryed -= ChangeIcon;
        }
    }

    private void ChangeIcon(BossEntry currentEntry, BossEntry nextEntry)
    {
        if (_imgBossIcon == null) return;
        _imgBossIcon.sprite = nextEntry?.Icon;
        _imgBossIcon.enabled = _imgBossIcon.sprite != null;
    }
}
