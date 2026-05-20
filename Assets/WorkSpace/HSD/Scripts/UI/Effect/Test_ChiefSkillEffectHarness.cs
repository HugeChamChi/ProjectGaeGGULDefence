using UnityEngine;
using Cysharp.Threading.Tasks;

namespace HSD.UI.Effect.Tests
{
    public class Test_ChiefSkillEffectHarness : MonoBehaviour
    {
        [Header("References")]
        public UI_ChiefSkillEffect effectView;
        
        [Header("Test Data")]
        public Sprite testChiefSprite;

        private ChiefSkillEffectPresenter _presenter;

        private void Start()
        {
            if (effectView != null)
            {
                // Set the view to be initially disabled as per typical UI setups
                effectView.gameObject.SetActive(false);
                _presenter = new ChiefSkillEffectPresenter(effectView);
            }
        }

        [Button]
        public void PlayChiefSkillEffect()
        {
            if (_presenter == null)
            {
                Debug.LogError("Test_ChiefSkillEffectHarness: Presenter or View is not assigned.");
                return;
            }
            
            _presenter.ExecuteSkillEffectAsync(testChiefSprite, this.GetCancellationTokenOnDestroy()).ContinueWith(() =>
            {
                Debug.Log("Test_ChiefSkillEffectHarness: Chief Skill Effect Test Completed Successfully.");
            }).Forget();
        }
    }
}
