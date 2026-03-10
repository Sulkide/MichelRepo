public sealed class WeaknessState : PlayerState
{
    public override PlayerStatusType Type => PlayerStatusType.Weakness;

    private readonly float _duration;
    private float _elapsed;

    public WeaknessState(float duration)
    {
        _duration = duration;
    }

    public override float OutgoingDamageMultiplier => 0.5f;

    public override void Tick(PlayerStatusController ctx, float dt)
    {
        _elapsed += dt;
        if (_elapsed >= _duration)
            ctx.SetState(new NormalState());
    }
}