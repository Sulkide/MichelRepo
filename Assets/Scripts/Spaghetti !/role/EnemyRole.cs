using UnityEngine;

public class EnemyRole : EntityRole
{
    public override EntityType RoleType => EntityType.Enemy;

    [Header("Enemy Params")]
    public float aggroRadius = 8f;
    public float chaseSpeedMult = 1.3f;

    public override void Tick(float dt)
    {
        if (entity.CurrentState == State.Walk)
        {
            float s = entity.Speed * chaseSpeedMult;
            if (entity.DesiredDirection != Direction.Still)
                entity.MoveInDirection(entity.DesiredDirection, s, dt);
        }
    }
}