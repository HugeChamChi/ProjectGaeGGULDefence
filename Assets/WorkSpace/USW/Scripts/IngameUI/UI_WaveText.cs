using UnityEngine;
using TMPro;
using GaeGGUL.Animation;
using Cysharp.Threading.Tasks;

/// <summary>
/// 현재 웨이브 정보를 텍스트로 표시하는 간단한 UI 컴포넌트입니다.
/// </summary>
public class UI_WaveText : MonoBehaviour
{
    private TMP_Text _text;
    private Anim_InOutBase _anim;

    private void Awake()
    {
        _text = GetComponent<TMP_Text>();
        _anim = GetComponent<Anim_InOutBase>();
    }

    private void OnEnable()
    {
        UpdateWaveText();
    }

    private void Start()
    {
        UpdateWaveText();
    }

    public void UpdateWaveText(int? waveNum = null)
    {
        if (_text == null) return;
        
        // 제공된 waveNum이 있으면 사용하고, 없으면 Manager.Wave에서 가져옴
        int currentWave = waveNum ?? ((Manager.Wave != null) ? Manager.Wave.CurrentWave + 1 : 1);
        _text.text = $"WAVE {currentWave}";

        if (_anim != null)
        {
            _anim.PlayInOut().Forget();
        }
    }
}
