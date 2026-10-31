using System;
using GGD.UI.Authoring;

namespace HSD.UI.Setting
{
    /// <summary>Same exported keys as Studio, connected to the existing production services and Presenter.</summary>
    public sealed class SettingsGameBindings : IAuthoringBindings, IDisposable
    {
        private readonly AudioManager _audio;
        private readonly GamePresentationSettings _settings;
        private readonly UI_SettingPresenter _presenter;
        private readonly UI_SettingPanel_Base _host;
        private readonly bool _ingame;
        private bool _closing;
        /// <inheritdoc />
        public event Action OnChanged;
        /// <summary>Diagnostic counter of actual user commands, never state refreshes.</summary>
        public int CommandCount { get; private set; }

        /// <summary>Uses injected scene/app dependencies; never reads a global manager or fake state.</summary>
        public SettingsGameBindings(AudioManager audio, GamePresentationSettings settings, UI_SettingPresenter presenter, UI_SettingPanel_Base host, bool ingame)
        {
            _audio = audio ?? throw new ArgumentNullException(nameof(audio)); _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _presenter = presenter; _host = host; _ingame = ingame; _settings.OnChanged += Publish;
        }
        /// <inheritdoc />
        public object Read(string key)
        {
            foreach (AudioGroup group in Enum.GetValues(typeof(AudioGroup)))
            {
                string prefix = "settings." + group.ToString().ToLowerInvariant();
                if (key == prefix + ".volume") return (float)_audio.GetVolume(group);
                if (key == prefix + ".enabled") return !_audio.IsMuted(group);
            }
            return key switch
            {
                "settings.damageNumbers" => _settings.ShowDamageNumbers,
                "settings.vibration" => _settings.VibrationEnabled,
                "settings.frameRate" => (float)CurrentFrame(),
                "settings.frame.canPrevious" => CurrentFrame() == 60,
                "settings.frame.canNext" => CurrentFrame() == 30,
                "context.ingame" => _ingame,
                _ => throw new ArgumentException("Unsupported settings state " + key)
            };
        }
        /// <inheritdoc />
        public void Execute(string key, object argument = null)
        {
            foreach (AudioGroup group in Enum.GetValues(typeof(AudioGroup)))
            {
                string prefix = "settings." + group.ToString().ToLowerInvariant();
                if (key == prefix + ".setVolume")
                {
                    float value = Convert.ToSingle(argument);
                    if (float.IsNaN(value) || float.IsInfinity(value) || value < 0 || value > 100) throw new ArgumentOutOfRangeException(nameof(argument));
                    _presenter.OnVolumeChanged(group, UnityEngine.Mathf.RoundToInt(value)); Record(); return;
                }
                if (key == prefix + ".setEnabled") { _presenter.OnMuteChanged(group, !RequireBool(argument)); Record(); return; }
            }
            switch (key)
            {
                case "settings.setDamageNumbers": _presenter.OnDamageFloaterChanged(RequireBool(argument)); break;
                case "settings.setVibration": _presenter.OnVibrationChanged(RequireBool(argument)); break;
                case "settings.frame.previous": if (CurrentFrame() == 60) UnityEngine.Application.targetFrameRate = 30; break;
                case "settings.frame.next": if (CurrentFrame() == 30) UnityEngine.Application.targetFrameRate = 60; break;
                case "settings.close": if (_closing) return; _closing = true; _host.Close(); break;
                case "settings.restart": if (!_ingame) throw new InvalidOperationException("Restart is unavailable in lobby"); _presenter.OnRestartClicked(); break;
                case "settings.lobby": if (!_ingame) throw new InvalidOperationException("Already in lobby"); _presenter.OnGoToLobbyClicked(); break;
                default: throw new ArgumentException("Unsupported settings command " + key);
            }
            Record();
        }
        /// <summary>Refreshes current services on open without sending any command or saving.</summary>
        public void OnOpen() { _closing = false; Publish(); }
        private void Record() { CommandCount++; Publish(); }
        private void Publish() { OnChanged?.Invoke(); }
        private static int CurrentFrame() => Math.Abs(UnityEngine.Application.targetFrameRate - 60) < Math.Abs(UnityEngine.Application.targetFrameRate - 30) ? 60 : 30;
        private static bool RequireBool(object value) => value is bool result ? result : throw new ArgumentException("Expected bool");
        /// <summary>Releases the app-service subscription at the host's lifetime boundary.</summary>
        public void Dispose() { _settings.OnChanged -= Publish; OnChanged = null; }
    }
}
