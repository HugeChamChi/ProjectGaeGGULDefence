using System.Collections.Generic;
using UnityEngine;

namespace GaeGGUL.Tutorial
{
    public static class TutorialRegistry
    {
        private static Dictionary<string, TutorialActor> _actors = new Dictionary<string, TutorialActor>();
        private static Dictionary<string, UI_TutorialTarget> _uiTargets = new Dictionary<string, UI_TutorialTarget>();

        public static void RegisterActor(string id, TutorialActor actor)
        {
            if (string.IsNullOrEmpty(id)) return;
            _actors[id] = actor;
        }

        public static void UnregisterActor(string id)
        {
            if (_actors.ContainsKey(id)) _actors.Remove(id);
        }

        public static TutorialActor GetActor(string id)
        {
            return _actors.TryGetValue(id, out var actor) ? actor : null;
        }

        public static void RegisterUI(string id, UI_TutorialTarget ui)
        {
            if (string.IsNullOrEmpty(id)) return;
            _uiTargets[id] = ui;
        }

        public static void UnregisterUI(string id)
        {
            if (_uiTargets.ContainsKey(id)) _uiTargets.Remove(id);
        }

        public static UI_TutorialTarget GetUI(string id)
        {
            return _uiTargets.TryGetValue(id, out var ui) ? ui : null;
        }

        public static void Clear()
        {
            _actors.Clear();
            _uiTargets.Clear();
        }
    }
}
