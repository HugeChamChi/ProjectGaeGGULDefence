/// <summary>Nonattacking unit used only in the isolated Lizard scene to inspect real unit stat modifiers.</summary>
public sealed class LizardPreviewUnit : UnitBase
{
    /// <inheritdoc/>
    public override bool CanBasicAttack => false;
    /// <inheritdoc/>
    public override bool CanUseSkill => false;
}
