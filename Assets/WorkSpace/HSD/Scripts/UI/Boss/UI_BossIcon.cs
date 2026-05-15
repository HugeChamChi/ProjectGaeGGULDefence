using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UI_BossIcon : MonoBehaviour
{
    [SerializeField] private Image _imgBossIcon;

    private void Start()
    {
        Manager.Boss.OnBossEntryed += ChangeIcon;
    }

    private void OnDestroy()
    {
        Manager.Boss.OnBossEntryed -= ChangeIcon;
    }

    private void ChangeIcon(BossEntry entry)
    {
        _imgBossIcon.sprite = entry.bossIcon;
    }
}
