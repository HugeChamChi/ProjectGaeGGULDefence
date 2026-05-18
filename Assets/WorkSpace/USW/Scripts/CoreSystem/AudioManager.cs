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

        // 3. 저장된 데이터 로드
        LoadSettings();
    }

    #region Volume Control

    public void SetVolume(AudioGroup group, int volume)
    {
        _provider?.SetVolume(group, volume);
        SaveSettings();
    }

    public int GetVolume(AudioGroup group) => _provider?.GetVolume(group) ?? 0;

    #endregion

    #region Mute Control

    public void SetMute(AudioGroup group, bool isMute)
    {
        _provider?.SetMute(group, isMute);
        SaveSettings();
        
        Debug.Log($"[AudioManager] {group} Mute: {isMute}");
    }

    public bool IsMuted(AudioGroup group)
    {
        return _provider?.IsMuted(group) ?? false;
    }

    #endregion

    #region Persistence

    private const string VOL_KEY_PREFIX = "Audio_Vol_";
    private const string MUTE_KEY_PREFIX = "Audio_Mute_";

    private void SaveSettings()
    {
        foreach (AudioGroup group in System.Enum.GetValues(typeof(AudioGroup)))
        {
            PlayerPrefs.SetInt($"{VOL_KEY_PREFIX}{group}", GetVolume(group));
            PlayerPrefs.SetInt($"{MUTE_KEY_PREFIX}{group}", IsMuted(group) ? 1 : 0);
        }
        PlayerPrefs.Save();
    }

    private void LoadSettings()
    {
        foreach (AudioGroup group in System.Enum.GetValues(typeof(AudioGroup)))
        {
            // 볼륨 로드 (기본값 100)
            int savedVol = PlayerPrefs.GetInt($"{VOL_KEY_PREFIX}{group}", 100);
            _provider?.SetVolume(group, savedVol);

            // 뮤트 로드 (기본값 0=false)
            bool savedMute = PlayerPrefs.GetInt($"{MUTE_KEY_PREFIX}{group}", 0) == 1;
            _provider?.SetMute(group, savedMute);
        }
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
        if (string.IsNullOrEmpty(address)) return;

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
