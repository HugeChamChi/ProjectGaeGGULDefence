using UnityEngine;

/// <summary>
/// 오디오 엔진의 핵심 기능을 정의하는 인터페이스
/// 향후 FMOD, Wwise 등 다른 엔진으로 교체 시 이 인터페이스만 구현하면 됨
/// </summary>
public interface IAudioProvider
{
    void Init();
    void PlayBGM(AudioClip clip);
    void PlaySFX(AudioClip clip);
    void StopBGM();
    void SetVolume(AudioGroup group, int volume);
    int GetVolume(AudioGroup group);
    void SetMute(AudioGroup group, bool isMute);
    bool IsMuted(AudioGroup group);
}
