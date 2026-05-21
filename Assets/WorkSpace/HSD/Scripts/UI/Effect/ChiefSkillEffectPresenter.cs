using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace HSD.UI.Effect
{
    public class ChiefSkillEffectPresenter
    {
        private readonly UI_ChiefSkillEffect _view;

        public ChiefSkillEffectPresenter(UI_ChiefSkillEffect view)
        {
            _view = view;
        }

        public async UniTask ExecuteSkillEffectAsync(Sprite chiefSprite, CancellationToken ct)
        {
            if (_view == null)
            {
                Debug.LogError("ChiefSkillEffectPresenter: View is null.");
                return;
            }

            // 뷰 활성화
            await _view.OpenAsync();

            // 이펙트 애니메이션 실행 (View에서 전달받은 조건대로 동작)
            await _view.PlayEffectAsync(chiefSprite, ct);

            // 뷰 비활성화
            await _view.CloseAsync();
        }
    }
}
