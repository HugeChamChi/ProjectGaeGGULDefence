using VContainer;
public class UnitSoundController
{
    private AudioManager _audioManager;

    private UnitBase _owner;

    public UnitSoundController(UnitBase owner, AudioManager audioManager)
    {
        _owner = owner;
        _audioManager = audioManager;
        _owner.onAttack.AddListener(PlayAttackSound);
    }

    private void PlayAttackSound()
    {
        _audioManager.PlaySFX(_owner.unitData.attackSoundAddress);
    }
}
