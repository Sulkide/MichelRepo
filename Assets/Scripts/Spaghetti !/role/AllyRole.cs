using UnityEngine;

public class AllyRole : EntityRole
{
    public override EntityType RoleType => EntityType.Ally;

    [Header("Ally Params")]
    public float followDistance = 5f;
    public float assistSpeedMult = 1.1f;

    public override void Tick(float dt)
    {

        if (entity.DesiredDirection != Direction.Still)
        {
            entity.MoveInDirection(entity.DesiredDirection, entity.Speed * assistSpeedMult, dt);
            entity.SetState(State.Walk);
        }
        else
        {
            entity.SetState(State.Idle);
        }
    }
}