using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace HSD.InGameDebug
{
    public class UI_DebugTotemGridView : MonoBehaviour
    {
        private List<UI_DebugTotemGridSlot> _slots = new List<UI_DebugTotemGridSlot>();
        private bool _isInit = false;

        public void InitOrRefresh(UI_DebugTotemPopup detailPopup)
        {
            var gm = FindObjectOfType<GridManager>(true);
            if (gm == null) return;

            if (!_isInit)
            {
                var glg = gameObject.GetComponent<GridLayoutGroup>();
                if (glg == null)
                {
                    glg = gameObject.AddComponent<GridLayoutGroup>();
                    glg.cellSize = new Vector2(50, 50);
                    glg.spacing = new Vector2(5, 5);
                    glg.childAlignment = TextAnchor.MiddleCenter;
                }

                glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                glg.constraintCount = gm.Columns;

                for (int z = 0; z < gm.Rows; z++)
                {
                    for (int x = 0; x < gm.Columns; x++)
                    {
                        var slotGo = new GameObject($"Slot_{x}_{z}");
                        slotGo.transform.SetParent(transform, false);
                        var slot = slotGo.AddComponent<UI_DebugTotemGridSlot>();
                        slot.Init(x, z, gm, detailPopup);
                        _slots.Add(slot);
                    }
                }
                _isInit = true;
            }

            foreach (var slot in _slots)
            {
                slot.Refresh();
            }
        }
    }
}
