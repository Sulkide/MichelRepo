public sealed class SleepyState : PlayerState
{
    public override PlayerStatusType Type => PlayerStatusType.Sleepy;

    private readonly float _duration;
    private float _elapsed;

    public SleepyState(float duration)
    {
        _duration = duration;
    }

    public override bool CanMove => false;

    public override void Tick(PlayerStatusController ctx, float dt)
    {
        _elapsed += dt;
        if (_elapsed >= _duration)
            ctx.SetState(new NormalState());
    }
}