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
    public AudioMixerGroup MixerGroup; // 해당 그룹의 믹서 그룹

    private int _volume = 100;
    public int Volume 
    { 
        get => _volume;
        set => _volume = Mathf.Clamp(value, 0, 100);
    }

    public AudioSource Source { get; set; }

    public AudioGroupData(AudioGroup groupType, string mixerParameter, AudioMixerGroup mixerGroup)
    {
        GroupType = groupType;
        MixerParameter = mixerParameter;
        MixerGroup = mixerGroup;
        Volume = 100;
    }

    /// <summary>
    /// 설정된 볼륨을 AudioMixer에 적용 (로그 스케일 변환)
    /// </summary>
    public void Apply(AudioMixer mixer)
    {
        if (mixer == null || string.IsNullOrEmpty(MixerParameter)) return;

        // 볼륨 계산 (로그 스케일: 0 -> -80dB, 100 -> 0dB)
        // 시니어 팁: 0.0001f 수준의 감쇄를 사용하여 로그(0) 에러 방지 및 자연스러운 감쇄 처리
        float volumePercent = Volume / 100f;
        float db = volumePercent <= 0 ? -80f : Mathf.Log10(Mathf.Max(volumePercent, 0.0001f)) * 20f;

        mixer.SetFloat(MixerParameter, db);
    }
}

