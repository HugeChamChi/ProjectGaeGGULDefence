using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using BackEnd;
using Cysharp.Threading.Tasks;
using LitJson;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GGD.BackendCharts
{
    /// <summary>How many numbers one key holds.</summary>
    public enum BalanceShape
    {
        /// <summary>One number in the Value column.</summary>
        Single,
        /// <summary>One number per unit tier in the Normal, Rare, Epic, Legend columns.</summary>
        Tiered,
        /// <summary>Any count of numbers in the Value column, separated by '|'.</summary>
        List
    }

    /// <summary>Allowed range and shape of one key. Every number of the key is checked.</summary>
    public readonly struct BalanceRule
    {
        public readonly float Min, Max;
        public readonly BalanceShape Shape;
        public readonly bool WholeNumber;
        public bool IsList => Shape == BalanceShape.List;
        public BalanceRule(float min, float max, BalanceShape shape = BalanceShape.Single, bool wholeNumber = false)
        { Min = min; Max = max; Shape = shape; WholeNumber = wholeNumber; }
    }

    /// <summary>
    /// One key-value balance chart on BACKND (DEV_/LIVE_ + content name). Shared by RunBalance, SkillBalance and later charts (TODO G12).
    /// Gameplay passes the bundled ScriptableObject value and receives the chart value when the chart is installed.
    /// The tutorial and direct Editor play keep bundled values (user decision 2026-11-11). A Player build that reads a key
    /// without the chart throws: login blocks until the chart is fetched, and there is no cached or bundled substitute.
    /// </summary>
    public sealed class BalanceChart
    {
        private static readonly string[] TierColumns = { "Normal", "Rare", "Epic", "Legend" };
        private const string TutorialSceneName = "TutorialScene";
        private const char ListSeparator = '|';

        [Serializable] private sealed class Settings { public bool UseRemote; }
        private readonly string _content, _settingsResource;
        private Dictionary<string, float[]> _values;
        private bool? _remoteRequired;
        private bool _warnedBundled;
        private int _generation;

        /// <summary>Every key the chart must contain. A missing, unknown, duplicate or out-of-range key rejects the whole chart.</summary>
        public IReadOnlyDictionary<string, BalanceRule> Rules { get; }
        public string ChartName => ChartEnvironment.Prefix + _content;
        public bool IsLoaded => _values != null;

        /// <param name="content">Chart name without the environment prefix, also the Excel sheet name.</param>
        /// <param name="settingsResource">Resources JSON with <c>{"UseRemote":true}</c>; when false or absent the chart is not used.</param>
        public BalanceChart(string content, string settingsResource, IReadOnlyDictionary<string, BalanceRule> rules)
        { _content = content; _settingsResource = settingsResource; Rules = rules; }

        public bool RequiresRemote
        {
            get
            {
                if (!_remoteRequired.HasValue)
                {
                    var settings = Resources.Load<TextAsset>(_settingsResource);
                    _remoteRequired = settings != null && JsonUtility.FromJson<Settings>(settings.text).UseRemote;
                }
                return _remoteRequired.Value;
            }
        }

        /// <summary>Chart value of a single-number key, or <paramref name="bundled"/> in the tutorial and direct Editor play.</summary>
        public float Number(string key, float bundled)
        {
            var numbers = Lookup(key, BalanceShape.Single);
            return numbers != null ? numbers[0] : bundled;
        }

        /// <summary>Chart value of a per-tier key for <paramref name="tier"/>, or <paramref name="bundled"/> in the tutorial and direct Editor play.</summary>
        public float Tiered(string key, Tier tier, float bundled)
        {
            var numbers = Lookup(key, BalanceShape.Tiered);
            int index = (int)tier;
            return numbers != null && index >= 0 && index < numbers.Length ? numbers[index] : bundled;
        }

        /// <summary>Chart value of a list key, or <paramref name="bundled"/> in the tutorial and direct Editor play.</summary>
        public float[] List(string key, float[] bundled) => Lookup(key, BalanceShape.List) ?? bundled;

        private float[] Lookup(string key, BalanceShape shape)
        {
            if (!Rules.TryGetValue(key, out var rule)) throw new ArgumentException("Unknown " + _content + " key: " + key, nameof(key));
            if (rule.Shape != shape) throw new ArgumentException(_content + " key " + key + " is " + rule.Shape + ", not " + shape, nameof(key));
            // Only a running tutorial keeps bundled values; in edit mode the open scene says nothing about gameplay.
            if (Application.isPlaying && SceneManager.GetActiveScene().name == TutorialSceneName) return null;
            if (_values != null) return _values[key];
            if (RequiresRemote)
            {
                if (!Application.isEditor) throw new InvalidOperationException(_content + " chart must load before gameplay reads " + key);
                if (!_warnedBundled)
                {
                    _warnedBundled = true;
                    Debug.LogWarning("[" + _content + "] No chart loaded (Editor play without login): using bundled values.");
                }
            }
            return null;
        }

        /// <summary>Validates chart rows. Throws <see cref="FormatException"/> naming the first problem.</summary>
        public Dictionary<string, float[]> Parse(JsonData rows)
        {
            if (rows == null || !rows.IsArray || rows.Count == 0) throw new FormatException(_content + ": no rows");
            var parsed = new Dictionary<string, float[]>();
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (!row.IsObject || !row.ContainsKey("Key")) throw new FormatException(_content + " row " + (i + 1) + ": Key column missing");
                string key = row["Key"]?.ToString().Trim();
                if (string.IsNullOrEmpty(key)) throw new FormatException(_content + " row " + (i + 1) + ": empty Key");
                if (!Rules.TryGetValue(key, out var rule)) throw new FormatException(_content + ": unknown key " + key);
                if (parsed.ContainsKey(key)) throw new FormatException(_content + ": duplicate key " + key);
                string[] parts;
                if (rule.Shape == BalanceShape.Tiered)
                {
                    parts = new string[TierColumns.Length];
                    for (int t = 0; t < TierColumns.Length; t++) parts[t] = Cell(row, TierColumns[t], key);
                }
                else
                {
                    string text = Cell(row, "Value", key);
                    parts = rule.Shape == BalanceShape.List ? text.Split(ListSeparator) : new[] { text };
                }
                var numbers = new float[parts.Length];
                for (int n = 0; n < parts.Length; n++)
                {
                    if (!float.TryParse(parts[n].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float number)
                        || float.IsNaN(number) || float.IsInfinity(number))
                        throw new FormatException(_content + ": " + key + " is not a number: " + parts[n]);
                    if (number < rule.Min || number > rule.Max)
                        throw new FormatException(_content + ": " + key + " out of range " + rule.Min + "~" + rule.Max + ": " + parts[n]);
                    if (rule.WholeNumber && number != Mathf.Floor(number))
                        throw new FormatException(_content + ": " + key + " must be a whole number: " + parts[n]);
                    numbers[n] = number;
                }
                parsed.Add(key, numbers);
            }
            foreach (var key in Rules.Keys)
                if (!parsed.ContainsKey(key)) throw new FormatException(_content + ": missing key " + key);
            return parsed;
        }

        private string Cell(JsonData row, string column, string key)
        {
            if (!row.ContainsKey(column)) throw new FormatException(_content + ": " + column + " column missing for " + key);
            string text = row[column]?.ToString().Trim();
            if (string.IsNullOrEmpty(text)) throw new FormatException(_content + ": empty value for " + key + " (" + column + ")");
            return text;
        }

        /// <summary>Fetches and installs the chart during login. Any failure throws and blocks entry.</summary>
        public async UniTask LoadAsync(CancellationToken token)
        {
            if (IsLoaded || !RequiresRemote) return;
            int session = _generation;
            if (!Backend.IsLogin) throw new InvalidOperationException(_content + " chart requires login");
            var owner = Backend.UserInDate;
            var rows = await BackendChartReader.ReadAsync(ChartName, token);
            var next = Parse(rows);
            if (session != _generation || !Backend.IsLogin || Backend.UserInDate != owner || token.IsCancellationRequested) throw new OperationCanceledException();
            if (IsLoaded) return;
            _values = next;
            Debug.Log("[" + _content + "] Loaded " + ChartName + ": " + next.Count + " keys");
        }

        /// <summary>Installs already validated values (login loader and Editor checks).</summary>
        public void Install(Dictionary<string, float[]> next) => _values = next;

        /// <summary>Forgets the chart: account change, logout and domain reload.</summary>
        public void Clear()
        {
            _generation++;
            _values = null;
            _remoteRequired = null;
            _warnedBundled = false;
        }
    }
}
