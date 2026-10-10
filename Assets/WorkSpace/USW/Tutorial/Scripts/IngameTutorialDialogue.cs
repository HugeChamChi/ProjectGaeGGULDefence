using DG.Tweening;
using TMPro;
using UnityEngine;

namespace GaeGGUL.Tutorial
{
    /// <summary>Unscaled speech-bubble motion and rich-text-safe typewriter for guided lessons.</summary>
    public sealed class IngameTutorialDialogue : MonoBehaviour
    {
        [SerializeField] private TMP_Text _progress;
        [SerializeField] private TMP_Text _instruction;
        [SerializeField] private TMP_Text _hint;
        [SerializeField, Min(1)] private float _charactersPerSecond = 38f;
        [SerializeField, Min(0.1f)] private float _popSeconds = 0.36f;
        private RectTransform _rect;
        private CanvasGroup _group;
        private Vector2 _restPosition;
        private Vector3 _restScale;
        private Sequence _entrance;
        private float _nextCharacter;
        private float _lastCharacterAt;
        private float _shownAt;
        private int _characterCount;
        private string _readyHint;
        private bool _glyphMoved;
        private Vector2 _defaultAnchorMin, _defaultAnchorMax;

        public bool IsTyping => gameObject.activeSelf && _instruction.maxVisibleCharacters < _characterCount;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _restPosition = _rect.anchoredPosition;
            _restScale = _rect.localScale;
            _defaultAnchorMin = _rect.anchorMin;
            _defaultAnchorMax = _rect.anchorMax;
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _hint.gameObject.SetActive(false);
        }

        public void AvoidAreas(IngameTutorialOverlay overlay, System.Collections.Generic.IReadOnlyList<System.Func<Rect>> targets)
        {
            if (_rect == null) return;
            _entrance?.Kill();
            _rect.localScale = _restScale;
            _group.alpha = 1;
            _rect.anchorMin = _defaultAnchorMin;
            _rect.anchorMax = _defaultAnchorMax;
            _rect.anchoredPosition = _restPosition;
            if (targets.Count == 0) return;
            float height = _defaultAnchorMax.y - _defaultAnchorMin.y;
            float bestOverlap = float.MaxValue;
            float bestY = 0.8f;
            foreach (float y in new[] { 0.8f, 0.62f, 0.44f, 0.26f, 0.08f })
            {
                _rect.anchorMin = new Vector2(_defaultAnchorMin.x, y);
                _rect.anchorMax = new Vector2(_defaultAnchorMax.x, Mathf.Min(0.98f, y + height));
                var bubble = overlay.ScreenRect(_rect);
                float overlap = 0;
                foreach (var target in targets)
                {
                    var area = target();
                    overlap += Mathf.Max(0, Mathf.Min(bubble.xMax, area.xMax + 16) - Mathf.Max(bubble.xMin, area.xMin - 16)) *
                        Mathf.Max(0, Mathf.Min(bubble.yMax, area.yMax + 16) - Mathf.Max(bubble.yMin, area.yMin - 16));
                }
                if (overlap < bestOverlap) { bestOverlap = overlap; bestY = y; }
                if (overlap == 0) break;
            }
            _rect.anchorMin = new Vector2(_defaultAnchorMin.x, bestY);
            _rect.anchorMax = new Vector2(_defaultAnchorMax.x, Mathf.Min(0.98f, bestY + height));
        }

        public void Show(IngameTutorialStep step, int total, int index = 0)
        {
            bool awareness = step.Stage == IngameTutorialStage.TimeLimit || step.Stage == IngameTutorialStage.ExperienceGauge ||
                step.Stage == IngameTutorialStage.LevelUp || step.Stage == IngameTutorialStage.ChiefSkill ||
                step.Stage == IngameTutorialStage.TotemChoice;
            ShowText(step.Instruction, index > 0 ? index : (int)step.Stage, total, awareness);
        }

        public void ShowText(string instruction, int index, int total, bool awareness)
        {
            gameObject.SetActive(true);
            _entrance?.Kill();
            _progress.text = $"첫 전투 가이드  ·  {index} / {total}";
            _instruction.text = instruction;
            _instruction.maxVisibleCharacters = int.MaxValue;
            _instruction.ForceMeshUpdate();
            _characterCount = _instruction.textInfo.characterCount;
            _instruction.maxVisibleCharacters = 0;
            _readyHint = awareness ? "화면을 누르면 계속해요" : "강조된 곳을 조작해 보세요";
            _hint.text = string.Empty;
            _shownAt = Time.unscaledTime;
            _nextCharacter = _shownAt + 0.12f;
            _rect.anchoredPosition = _restPosition + Vector2.down * 22f;
            _rect.localScale = Vector3.Scale(_restScale, new Vector3(0.86f, 0.72f, 1f));
            _group.alpha = 0;
            _entrance = DOTween.Sequence().SetUpdate(true)
                .Append(_rect.DOScale(_restScale, _popSeconds).SetEase(Ease.OutBack, 1.5f))
                .Join(_rect.DOAnchorPos(_restPosition, _popSeconds).SetEase(Ease.OutCubic))
                .Join(_group.DOFade(1f, 0.16f));
        }

        private void Update()
        {
            if (!IsTyping) return;
            float now = Time.unscaledTime;
            while (now >= _nextCharacter && _instruction.maxVisibleCharacters < _characterCount)
            {
                int index = _instruction.maxVisibleCharacters++;
                char character = _instruction.textInfo.characterInfo[index].character;
                _lastCharacterAt = now;
                _nextCharacter += (character == '.' || character == '!' || character == '?' ? 3f :
                    character == ',' ? 1.8f : 1f) / _charactersPerSecond;
            }
            if (!IsTyping) _hint.text = _readyHint;
        }

        private void LateUpdate()
        {
            if (_entrance != null && _entrance.IsActive() && _entrance.IsPlaying()) return;
            _rect.anchoredPosition = _restPosition + Vector2.up * (Mathf.Sin((Time.unscaledTime - _shownAt) * 2.4f) * 2f);
            // The newest glyph settles upwards into the line, including while gameplay is paused.
            float age = Time.unscaledTime - _lastCharacterAt;
            if (_instruction.maxVisibleCharacters <= 0) return;
            if (age > 0.12f)
            {
                if (_glyphMoved) _instruction.ForceMeshUpdate();
                _glyphMoved = false;
                return;
            }
            _instruction.ForceMeshUpdate();
            var info = _instruction.textInfo;
            var character = info.characterInfo[Mathf.Min(_instruction.maxVisibleCharacters, _characterCount) - 1];
            if (!character.isVisible) return;
            var vertices = info.meshInfo[character.materialReferenceIndex].vertices;
            float offset = -5f * Mathf.Pow(1f - Mathf.Clamp01(age / 0.12f), 2f);
            for (int i = 0; i < 4; i++) vertices[character.vertexIndex + i].y += offset;
            _instruction.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
            _glyphMoved = true;
        }

        /// <summary>The first awareness tap finishes the sentence; a subsequent tap advances.</summary>
        public void CompleteTyping()
        {
            _instruction.maxVisibleCharacters = _characterCount;
            _lastCharacterAt = float.NegativeInfinity;
            _instruction.ForceMeshUpdate();
            _hint.text = _readyHint;
        }

        public void Hide() => gameObject.SetActive(false);

        private void OnDisable()
        {
            _entrance?.Kill();
            if (_rect == null) return;
            _rect.anchoredPosition = _restPosition;
            _rect.localScale = _restScale;
        }
    }
}
