using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;
using GGD.UI.External;

namespace GGD.UI.Preview
{
    /// <summary>Memory-only settings and explicitly registered preview commands; never accesses game saves.</summary>
    public sealed class PreviewBindings : IUiBindings
    {
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>(StringComparer.Ordinal);
        private readonly Action<string> _record;
        /// <inheritdoc />
        public event Action OnChanged;
        /// <summary>Preview host handles window-local closing.</summary>
        public event Action OnCloseRequested;
        /// <summary>Preview host handles a bundled test tone.</summary>
        public event Action OnTestToneRequested;
        /// <summary>Identity of the authored fixture, used for state preservation.</summary>
        public string StateHash { get; private set; }
        /// <summary>Selected authored scenario name.</summary>
        public string Scenario { get; private set; }

        private PreviewBindings(Action<string> record) { _record = record; }

        /// <summary>Stages defaults; unchanged fixtures preserve compatible current values.</summary>
        public static PreviewBindings Prepare(UiContentPackage package, PreviewBindings previous, Action<string> record)
        {
            var result = new PreviewBindings(record) { StateHash = UiContentPackage.Hash(package.Bytes("state.xml")) };
            XElement root = UiXml.Parse(package.Text("state.xml"), "state.xml").Root;
            if (root.Name != "State") throw UiXml.Error(root, "Expected State");
            UiXml.Attributes(root, "scenario"); result.Scenario = UiXml.Required(root, "scenario");
            foreach (var node in root.Elements())
            {
                if (node.Name != "Value" || node.HasElements) throw UiXml.Error(node, "Expected Value");
                UiXml.Attributes(node, "key,type,value");
                string key = UiXml.Required(node, "key"), type = UiXml.Required(node, "type"), raw = UiXml.Required(node, "value");
                object value = type switch
                {
                    "number" => UiXml.Number(node, "value", 0),
                    "bool" => UiXml.Boolean(node, "value", false),
                    "string" => raw,
                    _ => throw UiXml.Error(node, "Unknown value type " + type)
                };
                if (previous != null && previous.StateHash == result.StateHash && previous.Scenario == result.Scenario &&
                    previous._values.TryGetValue(key, out var old) && old.GetType() == value.GetType()) value = old;
                if (!result._values.TryAdd(key, value)) throw UiXml.Error(node, "Duplicate state key");
            }
            if (!(result.Read("audio.masterVolume") is float volume) || volume < 0 || volume > 100 ||
                !(result.Read("audio.muted") is bool) || !(result.Read("preview.clicks") is float))
                throw UiXml.Error(root, "Required audio.masterVolume(number 0..100), audio.muted(bool), preview.clicks(number)");
            return result;
        }

        /// <inheritdoc />
        public object Read(string key) => _values.TryGetValue(key, out var value) ? value : throw new FormatException("Unknown state key " + key);

        /// <inheritdoc />
        public void Validate(string key, string command, Type argumentType)
        {
            if (key != null && argumentType != null && Read(key).GetType() != argumentType)
                throw new FormatException("Binding type mismatch: " + key);
            if (key != null) Read(key);
            if (command == null) return;
            string requiredKey;
            Type requiredType;
            switch (command)
            {
                case "audio.setMasterVolume": requiredKey = "audio.masterVolume"; requiredType = typeof(float); break;
                case "audio.setMuted": requiredKey = "audio.muted"; requiredType = typeof(bool); break;
                case "screen.close": case "preview.ping": case "audio.testTone": requiredKey = null; requiredType = null; break;
                default: throw new FormatException("Unknown command " + command);
            }
            if (requiredKey != key || requiredType != argumentType) throw new FormatException("Command/binding contract mismatch: " + command);
        }

        /// <inheritdoc />
        public void Execute(string command, object argument = null)
        {
            switch (command)
            {
                case "audio.setMasterVolume":
                    if (!(argument is float volume) || float.IsNaN(volume) || float.IsInfinity(volume) || volume < 0 || volume > 100)
                        throw new ArgumentException("Volume must be 0..100");
                    _values["audio.masterVolume"] = volume; break;
                case "audio.setMuted": _values["audio.muted"] = argument is bool muted ? muted : throw new ArgumentException("Mute must be bool"); break;
                case "preview.ping": _values["preview.clicks"] = (float)Read("preview.clicks") + 1; break;
                case "screen.close": OnCloseRequested?.Invoke(); break;
                case "audio.testTone": OnTestToneRequested?.Invoke(); break;
                default: throw new ArgumentException("Unknown command " + command);
            }
            _record(command + (argument == null ? "" : "(" + Convert.ToString(argument, CultureInfo.InvariantCulture) + ")"));
            OnChanged?.Invoke();
        }
    }
}
