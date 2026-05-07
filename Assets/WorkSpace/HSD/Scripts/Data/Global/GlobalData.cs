using UnityEngine;

public static class GlobalData
{
    public static ButtonReactionData ButtonReactionData;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        ButtonReactionData = RM.Load<ButtonReactionData>("GlobalData/ButtonReactionData");
    }
}
