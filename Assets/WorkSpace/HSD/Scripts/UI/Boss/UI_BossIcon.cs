using System.Collections;
using VContainer;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UI_BossIcon : MonoBehaviour
{
    [Inject] private BossManager _bossManager;

    [SerializeField] private Image _imgBossIcon;

    private void Start()
    {
        _bossManager.OnBossEntryed += ChangeIcon;
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
        _imgBossIcon.sprite = nextEntry.bossIcon;
    }
}
