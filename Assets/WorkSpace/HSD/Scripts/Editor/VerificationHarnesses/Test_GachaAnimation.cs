using System.Collections.Generic;
using GaeGGUL.Animation;
using UnityEngine;

namespace GaeGGUL.Test
{
    public class Test_GachaAnimation : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] private Anim_ListStaggered listStaggered;
        [SerializeField] private List<RectTransform> testSlots;

        [Header("Panel Animation")]
        [SerializeField] private Anim_InOutBase panelAnim;

        [Button("Test Play In")]
        public async void TestPlayIn()
        {
            Debug.Log("Starting Play In Test...");

            // 1. Panel Open
            if (panelAnim != null) await panelAnim.PlayIn();

            // 2. Slots Staggered In
            if (listStaggered != null)
            {
                await listStaggered.PlayInAsync(testSlots);
            }

            Debug.Log("Play In Test Finished.");
        }

        [Button("Test Skip")]
        public void TestSkip()
        {
            Debug.Log("Skipping Animations...");
            if (listStaggered != null) listStaggered.Skip();
            if (panelAnim != null) panelAnim.Complete();
        }

        [Button("Test Particle & Override")]
        public async void TestParticleOverride()
        {
            Debug.Log("Testing with Particles and Overrides...");
            if (listStaggered != null)
            {
                await listStaggered.PlayInAsync(testSlots);
            }
        }

        [Button("Test Play Out")]
        public async void TestPlayOut()
        {
            Debug.Log("Starting Play Out Test...");

            if (listStaggered != null)
            {
                await listStaggered.PlayOutAsync(testSlots);
            }

            if (panelAnim != null) await panelAnim.PlayOut();

            Debug.Log("Play Out Test Finished.");
        }
    }
}
