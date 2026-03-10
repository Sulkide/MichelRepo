public sealed class SlownessState : PlayerState
{
    public override PlayerStatusType Type => PlayerStatusType.Slowness;

    private readonly float _duration;
    private readonly float _speedMult;
    private float _elapsed;

    public SlownessState(float duration, float speedMult)
    {
        _duration = duration;
        _speedMult = speedMult;
    }

    public override float SpeedMultiplier => _speedMult;

    public override void Tick(PlayerStatusController ctx, float dt)
    {
        _elapsed += dt;
        if (_elapsed >= _duration)
            ctx.SetState(new NormalState());
    }
}