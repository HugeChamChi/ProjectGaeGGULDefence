using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace GaeGGUL.DeckUpgrade
{
    /// <summary>UI-only Spine preview: no combat prefab or gameplay scripts are instantiated.</summary>
    public sealed class DroneSpinePreview : MonoBehaviour
    {
        public SkeletonGraphic Graphic { get; private set; }
        public SkeletonAnimation Animation { get; private set; }
        private SkeletonDataAsset current;
        private Sprite currentSprite;
        private string currentIdle;

        public void Show(SkeletonDataAsset data, string idle, Material material, Material[] blendMaterials, Sprite fallback)
        {
            if ((Graphic != null && current == data) || (data == null && currentSprite == fallback && transform.childCount > 0)) return;
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject; child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            current = data; currentSprite = fallback; currentIdle = idle; Graphic = null; Animation = null;
            var host = (RectTransform)transform;
            if (data == null)
            {
                var portrait = new GameObject("SourceSpritePreview", typeof(RectTransform), typeof(Image));
                portrait.transform.SetParent(host, false);
                var rect = (RectTransform)portrait.transform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                var image = portrait.GetComponent<Image>(); image.sprite = fallback; image.preserveAspect = true; image.raycastTarget = false;
                return;
            }
            var components = SkeletonGraphic.NewSkeletonGraphicGameObject(data, host, material);
            Graphic = components.skeletonRenderer; Animation = components.skeletonAnimation;
            Graphic.name = "AnimatedSpineUnit"; Graphic.allowMultipleCanvasRenderers = true;
            Graphic.additiveMaterial = blendMaterials[0];
            Graphic.multiplyMaterial = blendMaterials[1];
            Graphic.screenMaterial = blendMaterials[2];
            // Follow the collection viewport's mask; popup previews have no such ancestor.
            Graphic.raycastTarget = false; Graphic.maskable = true;
            Graphic.RecalculateClipping();
            Animation.unscaledTime = true;
            Animation.AnimationState.SetAnimation(0, idle, true);
            Animation.UpdateComplete += HideGroundEffects;
            Animation.Update(0);
            HideGroundEffects(Graphic);
            Graphic.MatchRectTransformWithBounds();
            var bounds = Graphic.rectTransform.rect.size;
            float scale = Mathf.Min(host.rect.width / bounds.x, host.rect.height / bounds.y) * .9f;
            var rt = Graphic.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
            rt.localScale = Vector3.one * scale;
            rt.anchoredPosition = Vector2.Scale(rt.pivot - Vector2.one * .5f, bounds) * scale;
            foreach (var image in Graphic.GetComponentsInChildren<Graphic>()) image.raycastTarget = false;
        }

        public bool PlayAction(string name)
        {
            if (Animation == null || string.IsNullOrEmpty(name)) return false;
            var state = Animation.AnimationState;
            var data = Animation.Skeleton.Data;
            if (data.FindAnimation(name) == null) return false;
            Spine.TrackEntry last;
            if (data.FindAnimation(name + "_start") != null)
            {
                last = state.SetAnimation(0, name + "_start", false);
                var attack = state.AddAnimation(0, name, false, 0);
                attack.Delay = last.TrackComplete; last = attack;
            }
            else last = state.SetAnimation(0, name, false);
            if (data.FindAnimation(name + "_end") != null)
            {
                var ending = state.AddAnimation(0, name + "_end", false, 0);
                ending.Delay = last.TrackComplete; last = ending;
            }
            var idle = state.AddAnimation(0, currentIdle, true, 0); idle.Delay = last.TrackComplete;
            return true;
        }

        private void HideGroundEffects(ISkeletonRenderer animated)
        {
            // The UI supplies its own floor shadow; world-space hover glow is not part of the portrait.
            foreach (var slot in animated.Skeleton.Slots)
                if (slot.Data.Name == "hover_light" || slot.Data.Name == "shadow") slot.Pose.Attachment = null;
        }
    }
}
