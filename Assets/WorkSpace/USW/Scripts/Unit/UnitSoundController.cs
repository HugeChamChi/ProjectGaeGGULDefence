public class UnitSoundController
{
    private UnitBase _owner;

    public UnitSoundController(UnitBase owner)
    {
        _owner = owner;
        _owner.onAttack.AddListener(PlayAttackSound);
    }

    private void PlayAttackSound()
    {
        Manager.Audio.PlaySFX(_owner.unitData.attackSoundAddress);
    }
}
