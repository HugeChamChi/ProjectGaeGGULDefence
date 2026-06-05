using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HSD.InGameDebug
{
    public class UI_DebugTotemGridSlot : MonoBehaviour
    {
        private int _x, _z;
        private GridManager _gm;
        
        [SerializeField] private Image _bgImage;
        [SerializeField] private TextMeshProUGUI _txtDesc;
        [SerializeField] private Button _btn;

        private UI_DebugTotemPopup _detailPopup;

        public void Init(int x, int z, GridManager gm, UI_DebugTotemPopup detailPopup)
        {
            _x = x;
            _z = z;
            _gm = gm;
            _detailPopup = detailPopup;

            if (_bgImage == null) _bgImage = gameObject.AddComponent<Image>();
            
            if (_btn == null)
            {
                _btn = gameObject.AddComponent<Button>();
                _btn.onClick.AddListener(OnClickSlot);
            }
            else
            {
                _btn.onClick.RemoveListener(OnClickSlot);
                _btn.onClick.AddListener(OnClickSlot);
            }

            if (_txtDesc == null)
            {
                var txtGo = new GameObject("Text");
                txtGo.transform.SetParent(transform, false);
                var txtRt = txtGo.AddComponent<RectTransform>();
                txtRt.anchorMin = Vector2.zero;
                txtRt.anchorMax = Vector2.one;
                txtRt.offsetMin = Vector2.zero;
                txtRt.offsetMax = Vector2.zero;
                txtRt.sizeDelta = Vector2.zero;
                _txtDesc = txtGo.AddComponent<TextMeshProUGUI>();
                _txtDesc.fontSize = 14;
                _txtDesc.color = Color.white;
                _txtDesc.alignment = TextAlignmentOptions.Center;
                _txtDesc.enableAutoSizing = true;
            }
        }

        public void Refresh()
        {
            var cell = _gm.GetCell(_x, _z);
            if (cell == null) return;

            var m = cell.Model;
            bool hasBuff = m.TotemCellAttackBonus != 0 || m.TotemCellSpeedBonus != 0 || 
                           m.TotemCellFoodSpeedBonus != 0 || m.TotemCellFoodAmountBonus != 0 ||
                           m.TotemCellCritChanceBonus != 0 || m.TotemCellCritDamageBonus != 0 ||
                           m.TotemAttackDisabled || m.TotemAttackModifier != 1f || m.TotemSpeedModifier != 1f;

            if (cell.IsOccupied && cell.OccupyingTotem != null)
            {
                _bgImage.color = new Color(0.2f, 0.6f, 0.2f, 1f);
                _txtDesc.text = "T";
            }
            else if (hasBuff)
            {
                _bgImage.color = new Color(0.6f, 0.6f, 0.2f, 1f);
                _txtDesc.text = "B";
            }
            else
            {
                _bgImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);
                _txtDesc.text = $"{_x},{_z}";
            }
        }

        private void OnClickSlot()
        {
            var cell = _gm.GetCell(_x, _z);
            if (cell != null && _detailPopup != null)
            {
                _detailPopup.Show(cell.Model, new Vector2Int(_x, _z));
            }
        }

        private void OnDestroy()
        {
            if (_btn != null)
            {
                _btn.onClick.RemoveListener(OnClickSlot);
            }
        }
    }
}
