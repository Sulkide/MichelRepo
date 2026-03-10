public abstract class PlayerState
{
    public abstract PlayerStatusType Type { get; }
    
    public virtual bool CanMove => true;
    
    public virtual float SpeedMultiplier => 1f;
    public virtual float OutgoingDamageMultiplier => 1f;
    
    public virtual void Enter(PlayerStatusController ctx) { }
    public virtual void Exit(PlayerStatusController ctx)  { }
    public virtual void Tick(PlayerStatusController ctx, float dt) { }
}