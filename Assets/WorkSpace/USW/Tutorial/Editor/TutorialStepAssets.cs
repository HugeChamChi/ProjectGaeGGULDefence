using System.Linq;
using GaeGGUL.Tutorial;
using UnityEditor;

/// <summary>
/// Finds tutorial lesson assets by their gameplay stage. File names follow the play order
/// (2026-11-14), not the stage enum value, so authoring tools must never build paths from the enum.
/// </summary>
public static class TutorialStepAssets
{
    /// <summary>Folder that holds every tutorial lesson asset.</summary>
    public const string Folder = "Assets/WorkSpace/USW/Tutorial/Data/Steps";

    /// <summary>Path of the gameplay lesson for <paramref name="stage"/>, or null when none exists.</summary>
    public static string FindPath(IngameTutorialStage stage) =>
        AssetDatabase.FindAssets("t:IngameTutorialLesson", new[] { Folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(path =>
            {
                var lesson = AssetDatabase.LoadAssetAtPath<IngameTutorialLesson>(path);
                return lesson != null && lesson.Kind == IngameTutorialLesson.LessonKind.GameplayRecipe
                    && lesson.Gameplay != null && lesson.Gameplay.Stage == stage;
            });

    /// <summary>Gameplay lesson for <paramref name="stage"/>, or null when none exists.</summary>
    public static IngameTutorialLesson Find(IngameTutorialStage stage)
    {
        string path = FindPath(stage);
        return path == null ? null : AssetDatabase.LoadAssetAtPath<IngameTutorialLesson>(path);
    }

    /// <summary>Path for a new lesson asset. Rename it to its play-order number after inserting it into the sequence.</summary>
    public static string NewPath(IngameTutorialStage stage) => AssetDatabase.GenerateUniqueAssetPath($"{Folder}/{stage}.asset");
}
