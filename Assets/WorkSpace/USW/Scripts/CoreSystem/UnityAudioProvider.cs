using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;

/// <summary>
/// 유니티 기본 AudioSource와 AudioMixer를 사용하는 구현체
/// </summary>
public class UnityAudioProvider : IAudioProvider
{
    private AudioMixer _mainMixer;
    private Dictionary<AudioGroup, AudioGroupData> _groups;
    private GameObject _owner;

    public UnityAudioProvider(GameObject owner, AudioMixer mixer, List<AudioGroupData> groupList)
    {
        _owner = owner;
        _mainMixer = mixer;
        _groups = new Dictionary<AudioGroup, AudioGroupData>();

        foreach (var data in groupList)
        {
            if (!_groups.ContainsKey(data.GroupType))
                _groups.Add(data.GroupType, data);
        }
    }

    public void Init()
    {
        EnsureGroup(AudioGroup.BGM, true);
        EnsureGroup(AudioGroup.SFX, false);

        foreach (var group in _groups.Values)
        {
            group.Apply(_mainMixer);
        }
    }

    private void EnsureGroup(AudioGroup type, bool loop)
    {
        if (!_groups.TryGetValue(type, out var data)) return;

        if (data.Source == null)
        {
            var source = _owner.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            data.Source = source;
        }
    }

    public void PlayBGM(AudioClip clip)
    {
        if (!_groups.TryGetValue(AudioGroup.BGM, out var group)) return;
        if (group.Source.clip == clip && group.Source.isPlaying) return;

        group.Source.clip = clip;
        group.Source.Play();
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        if (_groups.TryGetValue(AudioGroup.SFX, out var group))
        {
            group.Source.PlayOneShot(clip);
        }
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
        if (_groups.TryGetValue(group, out var data))
        {
            data.Volume = Mathf.Clamp(volume, 0, 100);
            data.Apply(_mainMixer);
        }
    }

    public int GetVolume(AudioGroup group)
    {
        return _groups.TryGetValue(group, out var data) ? data.Volume : 0;
    }
}
