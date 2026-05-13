using UnityEngine;
using UnityEngine.Audio;
using System;

/// <summary>
/// 오디오 그룹별 데이터 (볼륨, 믹서 파라미터, 재생 소스 등)
/// </summary>
[Serializable]
public class AudioGroupData
{
    public AudioGroup GroupType;
    public string MixerParameter; // AudioMixer의 Exposed Parameter 이름
    public int Volume = 100;      // 0 ~ 100
    
    [HideInInspector]
    public AudioSource Source;

    /// <summary>
    /// 설정된 볼륨을 AudioMixer에 적용
    /// </summary>
    public void Apply(AudioMixer mixer)
    {
        if (mixer == null || string.IsNullOrEmpty(MixerParameter)) return;

        // 볼륨 계산 (로그 스케일: 0 -> -80dB, 100 -> 0dB)
        float db = Volume <= 0 ? -80f : Mathf.Log10(Volume / 100f) * 20f;
        mixer.SetFloat(MixerParameter, db);
    }
}
