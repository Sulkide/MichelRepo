public sealed class PoisonState : PlayerState
{
    public override PlayerStatusType Type => PlayerStatusType.Poison;

    private readonly float _duration;
    private readonly float _tickInterval;
    private readonly int _damagePerTick;

    private float _elapsed;
    private float _tick;

    public PoisonState(float duration, float tickInterval, int damagePerTick)
    {
        _duration = duration;
        _tickInterval = tickInterval;
        _damagePerTick = damagePerTick;
    }

    public override void Tick(PlayerStatusController ctx, float dt)
    {
        _elapsed += dt;
        _tick += dt;

        if (_tick >= _tickInterval)
        {
            _tick = 0f;
            ctx.ApplyDotDamage(_damagePerTick, leaveAtLeastOne: true);
        }

        if (_elapsed >= _duration)
            ctx.SetState(new NormalState());
    }
}