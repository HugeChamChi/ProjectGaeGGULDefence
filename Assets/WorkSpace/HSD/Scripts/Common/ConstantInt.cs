using System;
using UnityEngine.Scripting.APIUpdating;

/// <summary>등급과 무관하게 항상 같은 값을 반환한다.</summary>
[Serializable]
[DisplayName("고정값")]
[MovedFrom(true, sourceNamespace: null, sourceAssembly: "Assembly-CSharp", sourceClassName: "ConstantInt")]
public class ConstantInt : IScaledInt
{
    public int value;

    public int Get(Tier tier) => value;
}
