using System;
using UnityEngine;


public class EntitySpriteAnimator2D : MonoBehaviour
{

    public enum StateFilter
    {
        Any = -1,
        Idle = 0,
        Walk = 1,
        Run = 2, 
        Jump = 3, 
        InAir = 4,
        Punch = 5
    }

    public enum MoveFilter
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

    [Serializable]
    public class AnimRule
    {
        [Header("Condition")]
        public StateFilter state = StateFilter.Any;
        public MoveFilter move = MoveFilter.Any;

        [Header("Clip à jouer si la condition matche")]
        public SpriteAnimClip2D clip;
        public int priority = 0;
    }
    
    [Header("Références")]
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private BaseEntity entity;         
    public bool useRigidbodyVelocity = false;
    public float velocityMoveThreshold = 0.02f;
    
    [Header("Règles")]
    [SerializeField] private AnimRule[] rules;
    
    [Header("Options lecture globale")]
    [Min(0.01f)] public float globalSpeed = 1f;
    //public bool debugLog = false;

    private SpriteAnimClip2D _currentClip;
    private int _frameIndex;
    private float _frameTimer;
    private int _pingPongDir = 1;
    private int _lastRuleIndex = -1;

    private StateFilter _curState;
    private MoveFilter _curMove;

    private Rigidbody _rb;

    private void Reset()
    {
        if (!targetRenderer) targetRenderer = GetComponent<SpriteRenderer>();
        if (!entity) entity = GetComponentInParent<BaseEntity>();
    }

    private void Awake()
    {
        if (!targetRenderer) targetRenderer = GetComponent<SpriteRenderer>();
        if (!entity) entity = GetComponentInParent<BaseEntity>();
        _rb = entity ? entity.GetComponent<Rigidbody>() : GetComponentInParent<Rigidbody>();
    }

    private void OnEnable()
    {
        ForceReselect(true);
    }

    private void Update()
    {
        if (!entity || !targetRenderer) return;

        ReadConditions();
        SelectRuleIfNeeded();
        TickAnimation(Time.deltaTime);
    }
    
    private void ReadConditions()
    {
        _curState = ToStateFilter(entity.CurrentState);

        if (useRigidbodyVelocity && _rb != null)
        {
            Vector3 v = _rb.linearVelocity; v.y = 0f;
            if (v.magnitude >= velocityMoveThreshold)
                _curMove = ToMoveFilterFromVector(v.normalized);
            else
                _curMove = MoveFilter.Still;
        }
        else
        {
            _curMove = ToMoveFilter(entity.DesiredDirection);
        }
    }

    private static StateFilter ToStateFilter(State s)
    {
        switch (s)
        {
            default:
            case State.Idle:  return StateFilter.Idle;
            case State.Walk:  return StateFilter.Walk;
            case State.Run:   return StateFilter.Run;
            case State.Jump:  return StateFilter.Jump;
            case State.InAir: return StateFilter.InAir;
            case State.Punch: return StateFilter.Punch;
        }
    }

    private static MoveFilter ToMoveFilter(Direction d)
    {
        switch (d)
        {
            case Direction.Still:      return MoveFilter.Still;
            case Direction.Right:      return MoveFilter.Right;
            case Direction.UpRight:    return MoveFilter.UpRight;
            case Direction.Up:         return MoveFilter.Up;
            case Direction.UpLeft:     return MoveFilter.UpLeft;
            case Direction.Left:       return MoveFilter.Left;
            case Direction.DownLeft:   return MoveFilter.DownLeft;
            case Direction.Down:       return MoveFilter.Down;
            case Direction.DownRight:  return MoveFilter.DownRight;
            default:                   return MoveFilter.Still;
        }
    }

    private static MoveFilter ToMoveFilterFromVector(Vector3 dirXZNorm)
    {

        if (dirXZNorm.sqrMagnitude < 0.0001f) return MoveFilter.Still;

        float angle = Mathf.Atan2(dirXZNorm.x, dirXZNorm.z) * Mathf.Rad2Deg;
        if (angle < 0f) angle += 360f;
        if      (angle >= 337.5f || angle < 22.5f)   return MoveFilter.Up;
        else if (angle < 67.5f)   return MoveFilter.UpRight;
        else if (angle < 112.5f)  return MoveFilter.Right;
        else if (angle < 157.5f)  return MoveFilter.DownRight;
        else if (angle < 202.5f)  return MoveFilter.Down;
        else if (angle < 247.5f)  return MoveFilter.DownLeft;
        else if (angle < 292.5f)  return MoveFilter.Left;
        else                      return MoveFilter.UpLeft;
    }

    private void SelectRuleIfNeeded()
    {
        int bestIndex = -1;
        int bestScore = int.MinValue;

        for (int i = 0; i < (rules?.Length ?? 0); i++)
        {
            var r = rules[i];
            if (r == null || r.clip == null || !r.clip.IsValid) continue;

            int score = MatchScore(r, _curState, _curMove);
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
            else if (score == bestScore && bestIndex != -1)
            {
        
                if (r.priority > rules[bestIndex].priority) bestIndex = i;
                else if (r.priority == rules[bestIndex].priority) bestIndex = i; 
            }
        }

        if (bestIndex != _lastRuleIndex)
            ApplyRule(bestIndex);
    }

    private int MatchScore(AnimRule r, StateFilter st, MoveFilter mo)
    {
        int score = 0;

        if (r.state != StateFilter.Any)
        {
            if (r.state != st) return int.MinValue;
            score++;
        }

        if (r.move != MoveFilter.Any)
        {
            if (r.move != mo) return int.MinValue;
            score++;
        }
        
        score = score * 10 + r.priority;
        return score;
    }

    private void ApplyRule(int ruleIndex)
    {
        _lastRuleIndex = ruleIndex;

        if (ruleIndex < 0 || rules == null || ruleIndex >= rules.Length)
        {
            _currentClip = null;
            _frameIndex = 0;
            _frameTimer = 0;
            _pingPongDir = 1;
            return;
        }

        var rule = rules[ruleIndex];
        _currentClip = rule.clip;
        _frameTimer = 0f;
        _pingPongDir = 1;

        if (_currentClip != null && _currentClip.IsValid)
        {
            _frameIndex = _currentClip.randomStartFrame
                ? UnityEngine.Random.Range(0, _currentClip.frames.Length)
                : 0;

            targetRenderer.sprite = _currentClip.frames[_frameIndex];

        }
    }

    public void ForceReselect(bool resetFrame)
    {
        if (resetFrame) _lastRuleIndex = -1;
        SelectRuleIfNeeded();
    }
    
    private void TickAnimation(float dt)
    {
        if (_currentClip == null || !_currentClip.IsValid || targetRenderer == null) return;

        float fps = Mathf.Max(0.01f, _currentClip.framesPerSecond * globalSpeed);
        float spf = 1f / fps;

        _frameTimer += dt;

        while (_frameTimer >= spf)
        {
            _frameTimer -= spf;

            switch (_currentClip.playback)
            {
                case SpriteAnimClip2D.PlaybackMode.Loop:
                    _frameIndex = (_frameIndex + 1) % _currentClip.frames.Length;
                    break;

                case SpriteAnimClip2D.PlaybackMode.OneShot:
                    if (_frameIndex < _currentClip.frames.Length - 1)
                        _frameIndex++;
                    else if (!_currentClip.holdLastOnOneShot)
                        _frameIndex = 0;
                    break;

                case SpriteAnimClip2D.PlaybackMode.PingPong:
                    _frameIndex += _pingPongDir;
                    if (_frameIndex >= _currentClip.frames.Length)
                    {
                        _pingPongDir = -1;
                        _frameIndex = Mathf.Max(0, _currentClip.frames.Length - 2);
                    }
                    else if (_frameIndex < 0)
                    {
                        _pingPongDir = 1;
                        _frameIndex = Mathf.Min(1, _currentClip.frames.Length - 1);
                    }
                    break;
            }

            targetRenderer.sprite = _currentClip.frames[Mathf.Clamp(_frameIndex, 0, _currentClip.frames.Length - 1)];

        }
    }
}
