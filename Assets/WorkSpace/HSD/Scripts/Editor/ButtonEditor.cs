using System.Reflection;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MonoBehaviour), true)]
[CanEditMultipleObjects]
public class ButtonEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        var type = target.GetType();
        var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        foreach (var method in methods)
        {
            var buttonAttribute = method.GetCustomAttribute<ButtonAttribute>();
            if (buttonAttribute != null)
            {
                string buttonName = string.IsNullOrEmpty(buttonAttribute.ButtonName) 
                    ? method.Name 
                    : buttonAttribute.ButtonName;

                if (GUILayout.Button(buttonName))
                {
                    method.Invoke(target, null);
                }
            }
        }
    }
}
