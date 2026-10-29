using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace GGD.UI.Authoring.Studio
{
    /// <summary>Mock services implementing the exported production contract, with explicit command/error records.</summary>
    public sealed class StudioBindings : IAuthoringBindings
    {
        private readonly XElement _catalog;
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>();
        /// <inheritdoc />
        public event Action OnChanged;
        /// <summary>Observable mock command history.</summary>
        public List<string> Commands { get; } = new List<string>();
        /// <summary>Whether the mock screen has requested closing.</summary>
        public bool Closed { get; private set; }
        /// <summary>Reopens the mock screen while preserving current fixture values.</summary>
        public void Reopen() { Closed = false; OnChanged?.Invoke(); }
        /// <summary>Creates defaults only from the shipped typed contract.</summary>
        public StudioBindings(XElement catalog) { _catalog = catalog; Reset(false); }
        /// <inheritdoc />
        public object Read(string key) => _values.TryGetValue(key, out var value) ? value : throw new ArgumentException("Unknown state " + key);
        /// <summary>Changes one mock fixture value without issuing a command.</summary>
        public void Set(string key, object value) { var previous = Read(key); if (value?.GetType() != previous.GetType()) throw new ArgumentException("Fixture type mismatch"); _values[key] = value; OnChanged?.Invoke(); }
        /// <summary>Selects a lobby/ingame fixture; layout originals are unaffected.</summary>
        public void Reset(bool ingame)
        {
            _values.Clear();
            foreach (var item in _catalog.Elements("State"))
            {
                string raw = (string)item.Attribute("default") ?? "";
                _values[AuthoringWorkspace.Required(item, "key")] = (string)item.Attribute("type") switch
                { "number" => float.Parse(raw, CultureInfo.InvariantCulture), "bool" => bool.Parse(raw), "string" => raw, _ => throw new FormatException("Unknown state type") };
            }
            _values["context.ingame"] = ingame; Closed = false; Commands.Clear(); OnChanged?.Invoke();
        }
        /// <inheritdoc />
        public void Execute(string key, object argument = null)
        {
            var contract = _catalog.Elements("Command").FirstOrDefault(x => (string)x.Attribute("key") == key) ?? throw new ArgumentException("Unregistered mock command " + key);
            string type = (string)contract.Attribute("type");
            if (type == "bool" && !(argument is bool) || type == "number" && !(argument is float) || type == "none" && argument != null) throw new ArgumentException("Wrong mock argument type " + key);
            if (key.EndsWith(".setVolume", StringComparison.Ordinal))
            { float value = (float)argument; if (value < 0 || value > 100 || float.IsNaN(value)) throw new ArgumentOutOfRangeException(nameof(argument)); Set(key.Replace(".setVolume", ".volume"), value); }
            else if (key.EndsWith(".setEnabled", StringComparison.Ordinal)) Set(key.Replace(".setEnabled", ".enabled"), argument);
            else if (key == "settings.setDamageNumbers") Set("settings.damageNumbers", argument);
            else if (key == "settings.setVibration") Set("settings.vibration", argument);
            else if (key == "settings.frame.previous" || key == "settings.frame.next")
            { Set("settings.frameRate", key.EndsWith("previous", StringComparison.Ordinal) ? 30f : 60f); Set("settings.frame.canPrevious", (float)Read("settings.frameRate") == 60); Set("settings.frame.canNext", (float)Read("settings.frameRate") == 30); }
            else if (key == "settings.close") Closed = true;
            else if (key == "settings.restart" || key == "settings.lobby")
            { if (!(bool)Read("context.ingame")) throw new InvalidOperationException("Unavailable in lobby fixture"); Commands.Add("MOCK CONFIRM REQUIRED: " + key); }
            else throw new NotSupportedException("No mock adapter " + key);
            Commands.Add(key + (argument == null ? "" : "(" + Convert.ToString(argument, CultureInfo.InvariantCulture) + ")")); OnChanged?.Invoke();
        }
    }
}
