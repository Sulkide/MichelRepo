using UnityEngine;

public abstract class DirectionalMoveCommand : IMovement
{
    private Transform target;
    private Vector3 delta;

    protected DirectionalMoveCommand(Transform target, Vector3 delta)
    {
        this.target = target;
        this.delta = delta;
    }

    public void Move()
    {
        target.position += delta;
    }

    public void Revert()
    {
        target.position -= delta;
    }
}

#region les 8 direction assignier

public sealed class MoveUp : DirectionalMoveCommand
{
    public MoveUp(Transform t, float d) : base(t, Vector3.forward * d)
    {
        
    }
}

public sealed class MoveDown : DirectionalMoveCommand
{
    public MoveDown(Transform t, float d) : base(t, Vector3.back * d)
    {
        
    }
}

public sealed class MoveRight : DirectionalMoveCommand
{
    public MoveRight(Transform t, float d) : base(t, Vector3.right * d)
    {
        
    }
}
public sealed class MoveLeft : DirectionalMoveCommand   
{
    public MoveLeft(Transform t, float d) : base(t, Vector3.left * d)
    {
        
    } 
}

public sealed class MoveUpRight : DirectionalMoveCommand
{
    public MoveUpRight(Transform t, float d) : base(t, (Vector3.forward + Vector3.right).normalized * d)
    {
        
    }
}

public sealed class MoveUpLeft : DirectionalMoveCommand
{
    public MoveUpLeft(Transform t, float d) : base(t, (Vector3.forward + Vector3.left).normalized * d)
    {
        
    }
}

public sealed class MoveDownRight : DirectionalMoveCommand
{
    public MoveDownRight(Transform t, float d) : base(t, (Vector3.back + Vector3.right).normalized * d)
    {
        
    }
}

public sealed class MoveDownLeft : DirectionalMoveCommand
{
    public MoveDownLeft(Transform t, float d) : base(t, (Vector3.back + Vector3.left).normalized * d)
    {
        
    }
}

#endregion