using UnityEngine;

public abstract class MovementStrategy : ScriptableObject
{
    public abstract MovementMode Mode { get; }
    public abstract void Tick(BaseEntity entity, float deltaTime);
}