using UnityEngine;

[CreateAssetMenu(fileName = "FreeMoveStrategy", menuName = "NPC/Movement/Free")]
public class FreeMoveStrategy : MovementStrategy
{
    public override MovementMode Mode => MovementMode.Free;

    [Header("Free Mode")]
    public Vector2 changeDirInterval = new Vector2(1.2f, 2.8f);
    [Range(0f, 1f)] public float idleChance = 0.25f;
    public Vector2 idleDurationRange = new Vector2(0.6f, 1.6f);
    public float wanderRadius = 0f;

    private class Runtime
    {
        public float nextSwitchTime;
        public float idleUntil;
        public Vector3 spawnPos;
        public bool initialized;
    }


    private readonly System.Collections.Generic.Dictionary<int, Runtime> _runtimes = new System.Collections.Generic.Dictionary<int, Runtime>();

    public override void Tick(BaseEntity entity, float dt)
    {
        if (!_runtimes.TryGetValue(entity.GetInstanceID(), out var r))
        {
            r = new Runtime { initialized = true, spawnPos = entity.transform.position };
            r.nextSwitchTime = Time.time + Random.Range(changeDirInterval.x, changeDirInterval.y);
            _runtimes[entity.GetInstanceID()] = r;
        }
        
        if (Time.time < r.idleUntil)
        {
            entity.SetDesired(Direction.Still, State.Idle);
            return;
        }
        
        if (Time.time >= r.nextSwitchTime)
        {
            r.nextSwitchTime = Time.time + Random.Range(changeDirInterval.x, changeDirInterval.y);
            
            if (Random.value < idleChance)
            {
                r.idleUntil = Time.time + Random.Range(idleDurationRange.x, idleDurationRange.y);
                entity.SetDesired(Direction.Still, State.Idle);
                return;
            }
            
            var dir = DirectionUtil.Random8();
            
            if (wanderRadius > 0f)
            {
                var offset = entity.transform.position - r.spawnPos;
                if (offset.magnitude > wanderRadius)
                {
                    Vector3 toCenter = (r.spawnPos - entity.transform.position).normalized;
                    dir = ClosestDirection(toCenter);
                }
            }

            entity.SetDesired(dir, State.Walk);
        }
        
        if (entity.DesiredDirection != Direction.Still)
        {
            entity.MoveInDirection(entity.DesiredDirection, entity.Speed, dt);
            entity.SetState(State.Walk);
        }
        else
        {
            entity.SetState(State.Idle);
        }
    }

    private Direction ClosestDirection(Vector3 v)
    {
        v.y = 0f;
        if (v.sqrMagnitude < 0.0001f) return Direction.Still;

        v.Normalize();
        Direction best = Direction.Right;
        float bestDot = -Mathf.Infinity;

        for (int i = 1; i <= 8; i++)
        {
            var d = (Direction)i;
            var dv = DirectionUtil.ToVector(d);
            float dot = Vector3.Dot(v, dv);
            if (dot > bestDot) { bestDot = dot; best = d; }
        }
        return best;
    }
}
