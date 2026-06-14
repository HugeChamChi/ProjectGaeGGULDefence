using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;

public class AnimatorControllerGenerator : EditorWindow
{
    private AnimatorController baseController;
    private AnimatorController lastBaseController;

    [System.Serializable]
    public class ClipReplacement
    {
        public string pathName;
        public AnimationClip originalClip;
        public AnimationClip newClip;
    }

    private List<ClipReplacement> replacements = new List<ClipReplacement>();
    private Vector2 scrollPosition;
    private string targetPath = "Assets/GeneratedController.controller";

    [MenuItem("Tools/HSD/Animator Controller Generator")]
    public static void ShowWindow()
    {
        GetWindow<AnimatorControllerGenerator>("Anim Controller Gen");
    }

    private void OnGUI()
    {
        GUILayout.Label("Base Animator Controller Settings", EditorStyles.boldLabel);
        
        baseController = (AnimatorController)EditorGUILayout.ObjectField("Base Controller", baseController, typeof(AnimatorController), false);

        if (baseController != lastBaseController)
        {
            UpdateStateList();
            lastBaseController = baseController;
        }

        if (baseController != null)
        {
            GUILayout.Space(10);
            GUILayout.Label("States & Animation Clips", EditorStyles.boldLabel);

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(300));
            
            foreach (var rep in replacements)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(rep.pathName, GUILayout.Width(250));
                rep.newClip = (AnimationClip)EditorGUILayout.ObjectField(rep.newClip, typeof(AnimationClip), false);
                GUILayout.EndHorizontal();
            }
            
            GUILayout.EndScrollView();

            GUILayout.Space(10);
            GUILayout.Label("Output Settings", EditorStyles.boldLabel);
            
            GUILayout.BeginHorizontal();
            targetPath = EditorGUILayout.TextField("Target Path", targetPath);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string path = EditorUtility.SaveFilePanelInProject("Save Animator Controller", baseController.name + "_Variant", "controller", "Please enter a file name to save the controller to");
                if (!string.IsNullOrEmpty(path))
                {
                    targetPath = path;
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            if (GUILayout.Button("Generate", GUILayout.Height(30)))
            {
                GenerateController();
            }
        }
    }

    private void UpdateStateList()
    {
        replacements.Clear();
        if (baseController == null) return;

        foreach (var layer in baseController.layers)
        {
            ExtractStates(layer.name, layer.stateMachine);
        }
    }

    private void ExtractStates(string path, AnimatorStateMachine stateMachine)
    {
        foreach (var state in stateMachine.states)
        {
            ExtractMotion(path + "/" + state.state.name, state.state.motion);
        }

        foreach (var subStateMachine in stateMachine.stateMachines)
        {
            ExtractStates(path + "/" + subStateMachine.stateMachine.name, subStateMachine.stateMachine);
        }
    }

    private void ExtractMotion(string path, Motion motion)
    {
        if (motion is AnimationClip clip)
        {
            replacements.Add(new ClipReplacement { pathName = path, originalClip = clip, newClip = clip });
        }
        else if (motion is BlendTree tree)
        {
            for (int i = 0; i < tree.children.Length; i++)
            {
                ExtractMotion(path + "/Blend_" + i, tree.children[i].motion);
            }
        }
    }

    private void GenerateController()
    {
        if (string.IsNullOrEmpty(targetPath))
        {
            EditorUtility.DisplayDialog("Error", "Please specify a target path.", "OK");
            return;
        }

        string basePath = AssetDatabase.GetAssetPath(baseController);
        if (string.IsNullOrEmpty(basePath))
        {
            EditorUtility.DisplayDialog("Error", "Base controller path is invalid.", "OK");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<Object>(targetPath) != null)
        {
            AssetDatabase.DeleteAsset(targetPath);
        }

        if (AssetDatabase.CopyAsset(basePath, targetPath))
        {
            AnimatorController newController = AssetDatabase.LoadAssetAtPath<AnimatorController>(targetPath);
            if (newController != null)
            {
                int replacementIndex = 0;
                foreach (var layer in newController.layers)
                {
                    ReplaceClips(layer.stateMachine, ref replacementIndex);
                }

                EditorUtility.SetDirty(newController);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog("Success", $"New Animator Controller generated at {targetPath}", "OK");
            }
        }
        else
        {
            EditorUtility.DisplayDialog("Error", "Failed to copy base controller.", "OK");
        }
    }

    private void ReplaceClips(AnimatorStateMachine stateMachine, ref int replacementIndex)
    {
        foreach (var state in stateMachine.states)
        {
            Motion newMotion = ReplaceMotion(state.state.motion, ref replacementIndex);
            if (newMotion != state.state.motion)
            {
                state.state.motion = newMotion;
                EditorUtility.SetDirty(state.state);
            }
        }

        foreach (var subStateMachine in stateMachine.stateMachines)
        {
            ReplaceClips(subStateMachine.stateMachine, ref replacementIndex);
        }
    }

    private Motion ReplaceMotion(Motion motion, ref int replacementIndex)
    {
        if (motion is AnimationClip)
        {
            if (replacementIndex < replacements.Count)
            {
                var rep = replacements[replacementIndex++];
                if (rep.newClip != null) return rep.newClip;
            }
            return motion;
        }
        else if (motion is BlendTree tree)
        {
            ChildMotion[] children = tree.children;
            bool changed = false;
            for (int i = 0; i < children.Length; i++)
            {
                Motion newChildMotion = ReplaceMotion(children[i].motion, ref replacementIndex);
                if (newChildMotion != children[i].motion)
                {
                    children[i].motion = newChildMotion;
                    changed = true;
                }
            }
            if (changed)
            {
                tree.children = children;
                EditorUtility.SetDirty(tree);
            }
            return tree;
        }
        return motion;
    }
}
