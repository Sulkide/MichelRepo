using UnityEngine;

public enum EntityType
{
    Enemy, 
    Ally,
    Pnj
}

public enum MovementMode
{
    Free, 
    Restricted, 
    Follow, 
    Flee, 
    Fixed
}

public enum Direction
{
    Any = -1, 
    Still = 0,
    Right = 1, 
    UpRight = 2,
    Up = 3, 
    UpLeft = 4,
    Left = 5,
    DownLeft = 6, 
    Down = 7, 
    DownRight = 8
}

public enum State
{
    Any = -1, 
    Idle = 0,
    Walk = 1, 
    Run = 2,
    Jump = 3, 
    InAir = 4, 
    Punch = 5
}

public static class DirectionUtil
{

    public static Vector3 ToVector(Direction d)
    {
        switch (d)
        {
            case Direction.Right:     return new Vector3(1, 0, 0);
            case Direction.UpRight:   return new Vector3(1, 0, 1).normalized;
            case Direction.Up:        return new Vector3(0, 0, 1);
            case Direction.UpLeft:    return new Vector3(-1, 0, 1).normalized;
            case Direction.Left:      return new Vector3(-1, 0, 0);
            case Direction.DownLeft:  return new Vector3(-1, 0, -1).normalized;
            case Direction.Down:      return new Vector3(0, 0, -1);
            case Direction.DownRight: return new Vector3(1, 0, -1).normalized;
            default:                  return Vector3.zero;
        }
    }

    public static Direction Random8()
    {
        int r = Random.Range(1, 9);
        return (Direction)r;
    }
}