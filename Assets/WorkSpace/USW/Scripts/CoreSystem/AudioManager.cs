using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;

public enum AudioGroup
{
    BGM,
    SFX,
    Master
}

/// <summary>
/// 오디오 엔진(Unity/Native 등)에 상관없이 게임 비즈니스 로직을 처리하는 매니저
/// 10년차 시니어 팁: 리소스 로드 자동화 및 Strategy 패턴을 통한 유연한 확장성 확보
/// </summary>
public class AudioManager : Singleton<AudioManager>
{
    private const string BGM_PATH_ROOT = "Sound/BGM/";
    private const string SFX_PATH_ROOT = "Sound/SFX/";
    private const string MIXER_RESOURCE_PATH = "Master"; // Resources 내 AudioMixer 경로

    // 실제 기능을 수행하는 구현체 (Interface)
    private IAudioProvider _provider;

    #region Properties (Convenience for Master/BGM/SFX)

    public int MasterVolume
    {
        get => GetVolume(AudioGroup.Master);
        set => SetVolume(AudioGroup.Master, value);
    }

    public int BGMVolume
    {
        get => GetVolume(AudioGroup.BGM);
        set => SetVolume(AudioGroup.BGM, value);
    }

    public int SFXVolume
    {
        get => GetVolume(AudioGroup.SFX);
        set => SetVolume(AudioGroup.SFX, value);
    }

    #endregion

    protected override void Awake()
    {
        base.Awake();
        InitializeEngine();
    }

    private void InitializeEngine()
    {
        // 1. AudioMixer 로드 (Resources)
        AudioMixer mainMixer = RM.Load<AudioMixer>(MIXER_RESOURCE_PATH);
        
        if (mainMixer == null)
        {
            Debug.LogError($"[AudioManager] Failed to load AudioMixer at Resources/{MIXER_RESOURCE_PATH}");
            return;
        }

        // 2. 구현체 초기화
        _provider = new UnityAudioProvider(gameObject, mainMixer);
        _provider.Init();
    }

    #region Volume Control

    public void SetVolume(AudioGroup group, int volume) => _provider?.SetVolume(group, volume);
    public int GetVolume(AudioGroup group) => _provider?.GetVolume(group) ?? 0;

    #endregion

    #region Mute Control

    private Dictionary<AudioGroup, bool> _muteStates = new Dictionary<AudioGroup, bool>();

    public void SetMute(AudioGroup group, bool isMute)
    {
        _muteStates[group] = isMute;
        
        // Mute 상태에 따라 볼륨 직접 제어
        if (isMute)
            _provider?.SetVolume(group, 0);
        else
            _provider?.SetVolume(group, GetVolume(group));
        
        Debug.Log($"[AudioManager] {group} Mute: {isMute}");
    }

    public bool IsMuted(AudioGroup group)
    {
        return _muteStates.TryGetValue(group, out bool isMute) && isMute;
    }

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

    public void PlayBGM(AudioClip clip) => _provider?.PlayBGM(clip);
    public void StopBGM() => _provider?.StopBGM();

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

    public void PlaySFX(AudioClip clip) => _provider?.PlaySFX(clip);

    #endregion
}
