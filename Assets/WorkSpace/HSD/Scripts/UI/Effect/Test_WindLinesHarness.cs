using UnityEngine;
using UnityEngine.UI;

namespace HSD.Test
{
    public class Test_WindLinesHarness : MonoBehaviour
    {
        [Header("Target UI")]
        public Image targetImage;
        public Shader windLinesShader;

        [Header("Settings")]
        public Color color = Color.white;
        public float speed = 2.0f;
        public float density = 20.0f;
        public float lineLength = 0.5f;
        public float lineThickness = 0.1f;

        [Button("Setup Material")]
        public void SetupMaterial()
        {
            if (targetImage == null)
            {
                Debug.LogError("Target Image is null!");
                return;
            }

            if (windLinesShader == null)
            {
                windLinesShader = Shader.Find("UI/WindLines");
            }

            if (windLinesShader == null)
            {
                Debug.LogError("WindLines shader not found! Make sure it's named 'UI/WindLines'");
                return;
            }

            Material mat = new Material(windLinesShader);
            mat.name = "WindLines_RuntimeMat";
            UpdateMaterialProperties(mat);
            
            targetImage.material = mat;
            Debug.Log("Successfully setup material on " + targetImage.name);
        }

        [Button("Update Properties")]
        public void UpdateProperties()
        {
            if (targetImage != null && targetImage.material != null)
            {
                UpdateMaterialProperties(targetImage.material);
                Debug.Log("Updated material properties.");
            }
        }

        private void UpdateMaterialProperties(Material mat)
        {
            mat.SetColor("_Color", color);
            mat.SetFloat("_Speed", speed);
            mat.SetFloat("_Density", density);
            mat.SetFloat("_LineLength", lineLength);
            mat.SetFloat("_LineThickness", lineThickness);
        }

        [Button("Preset: Fast & Sparse")]
        public void PresetFastSparse()
        {
            speed = 5.0f;
            density = 10.0f;
            lineLength = 0.8f;
            lineThickness = 0.05f;
            UpdateProperties();
        }

        [Button("Preset: Slow & Dense")]
        public void PresetSlowDense()
        {
            speed = 1.0f;
            density = 50.0f;
            lineLength = 0.3f;
            lineThickness = 0.15f;
            UpdateProperties();
        }
    }
}
