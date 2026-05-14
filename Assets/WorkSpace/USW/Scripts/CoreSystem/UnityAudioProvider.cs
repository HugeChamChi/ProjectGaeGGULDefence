using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;
using System;
using Unity.VisualScripting;

/// <summary>
/// 유니티 기본 AudioSource와 AudioMixer를 사용하는 구현체
/// </summary>
public class UnityAudioProvider : IAudioProvider
{
    private readonly AudioMixer _mainMixer;
    private readonly Dictionary<AudioGroup, AudioGroupData> _groups = new();
    private readonly GameObject _owner;

    public UnityAudioProvider(GameObject owner, AudioMixer mixer)
    {
        _owner = owner;
        _mainMixer = mixer;
    }

    public void Init()
    {
        if (_mainMixer == null)
        {
            Debug.LogError("[AudioManager] AudioMixer is null! Check Resources path.");
            return;
        }

        SetupGroups();
    }

    /// <summary>
    /// AudioGroup Enum을 기반으로 믹서 그룹과 파라미터를 자동으로 매칭 및 세팅
    /// </summary>
    private void SetupGroups()
    {
        foreach (AudioGroup groupType in Enum.GetValues(typeof(AudioGroup)))
        {
            string groupName = groupType.ToString();
            
            // 1. Mixer Group 찾기 (이름 매칭)
            AudioMixerGroup[] matchingGroups = _mainMixer.FindMatchingGroups(groupName);
            AudioMixerGroup mixerGroup = matchingGroups.Length > 0 ? matchingGroups[0] : null;

            if (mixerGroup == null && groupType != AudioGroup.Master)
            {
                Debug.LogWarning($"[AudioManager] No MixerGroup found for {groupName}. Using Master as fallback.");
                mixerGroup = _mainMixer.FindMatchingGroups("Master")[0];
            }

            // 2. Exposed Parameter 이름 규칙: {GroupName}_Vol
            // 시니어 팁: 파라미터 이름은 명시적인 규칙을 갖는 것이 유지보수에 유리함
            string parameterName = $"{groupName}_Vol";

            var data = new AudioGroupData(groupType, parameterName, mixerGroup);
            
            // 3. 전용 AudioSource 생성 (BGM, SFX 등)
            // Master는 볼륨 제어용이므로 Source를 생성하지 않음
            if (groupType != AudioGroup.Master)
            {
                var source = _owner.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.outputAudioMixerGroup = mixerGroup;
                source.loop = (groupType == AudioGroup.BGM);
                data.Source = source;
            }

            _groups[groupType] = data;
            
            // 초기 볼륨 적용
            data.Apply(_mainMixer);
        }
    }

    public void PlayBGM(AudioClip clip)
    {
        if (clip == null) return;
        if (!_groups.TryGetValue(AudioGroup.BGM, out var group)) return;
        
        // 동일한 클립이 이미 재생 중이면 무시
        if (group.Source.clip == clip && group.Source.isPlaying) return;

        group.Source.clip = clip;
        group.Source.Play();
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        if (!_groups.TryGetValue(AudioGroup.SFX, out var group)) return;

        // PlayOneShot은 가볍고 여러 클립을 겹쳐서 재생하기에 최적화됨
        group.Source.PlayOneShot(clip);
    }

    public void StopBGM()
    {
        if (_groups.TryGetValue(AudioGroup.BGM, out var group))
        {
            group.Source.Stop();
        }
    }

    public void SetVolume(AudioGroup group, int volume)
    {
        if (!_groups.TryGetValue(group, out var data)) return;
        
        // 최적화: 볼륨이 같으면 믹서 호출 방지
        if (data.Volume == volume) return;

        data.Volume = volume;
        data.Apply(_mainMixer);
    }

    public int GetVolume(AudioGroup group)
    {
        return _groups.TryGetValue(group, out var data) ? data.Volume : 0;
    }
}
