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

    public bool IsMuted { get; set; }

    public AudioSource Source { get; set; }

    public AudioGroupData(AudioGroup groupType, string mixerParameter, AudioMixerGroup mixerGroup)
    {
        GroupType = groupType;
        MixerParameter = mixerParameter;
        MixerGroup = mixerGroup;
        Volume = 100;
        IsMuted = false;
    }

    /// <summary>
    /// 설정된 볼륨을 AudioMixer에 적용 (로그 스케일 변환)
    /// </summary>
    public void Apply(AudioMixer mixer)
    {
        if (mixer == null || string.IsNullOrEmpty(MixerParameter)) return;

        // 볼륨 계산 (로그 스케일: 0 -> -80dB, 100 -> 0dB)
        // Mute 상태면 강제로 -80dB 적용
        float volumePercent = IsMuted ? 0 : Volume / 100f;
        float db = volumePercent <= 0 ? -80f : Mathf.Log10(Mathf.Max(volumePercent, 0.0001f)) * 20f;

        mixer.SetFloat(MixerParameter, db);
    }}

