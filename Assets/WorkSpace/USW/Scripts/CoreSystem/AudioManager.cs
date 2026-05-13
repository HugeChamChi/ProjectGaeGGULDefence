using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;

public enum AudioGroup
{
    BGM,
    SFX
}

/// <summary>
/// 오디오 엔진(Unity/FMOD 등)에 상관없이 게임 비즈니스 로직을 처리하는 매니저
/// Strategy 패턴을 사용하여 내부 엔진을 교체 가능하도록 설계
/// </summary>
public class AudioManager : Singleton<AudioManager>
{
    private const string BGM_PATH_ROOT = "Sound/BGM/";
    private const string SFX_PATH_ROOT = "Sound/SFX/";

    [Header("Engine Settings (Unity Native)")]
    [SerializeField] private AudioMixer mainMixer;
    [SerializeField] private List<AudioGroupData> groupDataList = new List<AudioGroupData>();

    // 실제 기능을 수행하는 구현체 (Interface)
    private IAudioProvider _provider;

    protected override void Awake()
    {
        base.Awake();
        
        // 초기 구현체는 Unity 기본 엔진으로 설정
        // 추후 FMOD로 교체 시 이 부분만 FMODProvider로 변경하면 됨
        _provider = new UnityAudioProvider(gameObject, mainMixer, groupDataList);
        _provider.Init();
    }

    #region Volume Control

    public void SetVolume(AudioGroup group, int volume) => _provider.SetVolume(group, volume);
    public int GetVolume(AudioGroup group) => _provider.GetVolume(group);

    #endregion

    #region BGM Methods

    public void PlayBGM(string address)
    {
        string fullPath = $"{BGM_PATH_ROOT}{address}";
        AudioClip clip = RM.Load<AudioClip>(fullPath);

        if (clip != null)
            PlayBGM(clip);
        else
            Debug.LogWarning($"[AudioManager] BGM 로드 실패: {fullPath}");
    }

    public void PlayBGM(AudioClip clip) => _provider.PlayBGM(clip);
    public void StopBGM() => _provider.StopBGM();

    #endregion

    #region SFX Methods

    public void PlaySFX(string address)
    {
        string fullPath = $"{SFX_PATH_ROOT}{address}";
        AudioClip clip = RM.Load<AudioClip>(fullPath);

        if (clip != null)
            PlaySFX(clip);
        else
            Debug.LogWarning($"[AudioManager] SFX 로드 실패: {fullPath}");
    }

    public void PlaySFX(AudioClip clip) => _provider.PlaySFX(clip);

    #endregion
}
