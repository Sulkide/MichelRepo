using UnityEngine;

public abstract class EntityRole : MonoBehaviour
{
    protected BaseEntity entity;

    public abstract EntityType RoleType { get; }

    public virtual void Initialize(BaseEntity e)
    {
        entity = e;
    }

    public virtual void Tick(float dt) { }
}