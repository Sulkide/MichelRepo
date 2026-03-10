using UnityEngine;

public sealed class PlayerStatsAdapter : IPlayerStatsTarget
{
    private readonly PlayerMovement _player;
    private readonly PlayerManager _mgr;

    public int PlayerId => _player != null ? _player.currentId : 0;

    public PlayerStatsAdapter(PlayerMovement player)
    {
        _player = player;
        _mgr = PlayerManager.Instance;
    }

    public void ApplyGlobalStatMultiplier(float mult)
    {
        if (_player == null || _mgr == null) return;
        if (mult <= 0f) return;
        
        _mgr.ApplyPickupMultiplier(PlayerId, mult);
    }

    public void ApplyConditionalSpeed()
    {
        if (_player == null || _mgr == null) return;

        float maxMag = _mgr.GetMaxMagazinFinal(PlayerId);
        float ergo   = _mgr.GetErgonomyFinal(PlayerId);
        
        float speedMult = (maxMag < ergo) ? 2f : 0.5f;
        
        _player.SetExternalSpeedMultiplier(speedMult);
    }
}