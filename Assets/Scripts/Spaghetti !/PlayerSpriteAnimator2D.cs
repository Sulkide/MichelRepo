using System;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class PlayerSpriteAnimator2D : MonoBehaviour
{

    public enum StateFilter
    {
        Any = -1,
        Idle,
        Walk,
        Run, 
        Jump,
        InAir, 
        Punch, 
        Shoot
    } 

    public enum FacingFilter
    {
        Any = -1,
        Nord, 
        NordEst, 
        Est, 
        SudEst, 
        Sud, 
        SudOuest,
        Ouest, 
        NordOuest
    }

    public enum MoveFilter
    {
        Any = -1,
        Still,
        Right,
        UpRight,
        Up, 
        UpLeft,
        Left,
        DownLeft,
        Down,
        DownRight
    }

    [Serializable]
    public class AnimRule
    {
        [Header("Condition")]
        public StateFilter state = StateFilter.Any;
        public FacingFilter facing = FacingFilter.Any;
        public MoveFilter move = MoveFilter.Any;

        [Header("Clip à jouer")]
        public SpriteAnimClip2D clip;
        public int priority = 0;
    }

    [Header("Référence")]
    [SerializeField] private SpriteRenderer targetRenderer;
    private PlayerManager playerManager;
    [SerializeField] private RadialCursor8Way radial;

    [Header("Règles")]
    [SerializeField] private AnimRule[] rules;

    [Header("Options lecture")]
    [Min(0.01f)] public float globalSpeed = 1f;
    //public bool debugLog = false;

    private SpriteAnimClip2D _currentClip;
    private int _frameIndex;
    private float _frameTimer;
    private int _pingPongDir = 1;
    private int _lastRuleIndex = -1;

    private StateFilter _curState;
    private FacingFilter _curFacing;
    private MoveFilter _curMove;

    private void Reset()
    {
        targetRenderer = GetComponent<SpriteRenderer>();
    }

    private void Awake()
    {
        if (!targetRenderer) targetRenderer = GetComponent<SpriteRenderer>();
        if (!playerManager) playerManager = PlayerManager.Instance;
        if (!radial) radial = FindObjectOfType<RadialCursor8Way>();
    }

    private void OnEnable()
    {
        ForceReselect(true);
        if (playerManager != null) playerManager.OnStateChanged += OnPlayerStateChanged;
    }

    private void OnDisable()
    {
        if (playerManager != null) playerManager.OnStateChanged -= OnPlayerStateChanged;
    }

    private void OnPlayerStateChanged(PlayerManager.PlayerState st)
    {
        ForceReselect(true);
    }

    private void Update()
    {
        ReadConditions();
        SelectRuleIfNeeded();
        TickAnimation(Time.deltaTime);
    }

    private void ReadConditions()
    {
        var pmState = playerManager ? playerManager.State : PlayerManager.PlayerState.Idle;
        _curState = ToStateFilter(pmState);

        if (radial)
        {
            _curFacing = ToFacingFilter(radial.CurrentFacing);
            _curMove = radial.IsMoving ? ToMoveFilter(radial.CurrentMoveDir) : MoveFilter.Still;
        }
        else
        {
            _curFacing = FacingFilter.Nord;
            _curMove = MoveFilter.Still;
        }
    }

    private static StateFilter ToStateFilter(PlayerManager.PlayerState s)
    {
        switch (s)
        {
            default:
            case PlayerManager.PlayerState.Idle:  return StateFilter.Idle;
            case PlayerManager.PlayerState.Walk:  return StateFilter.Walk;
            case PlayerManager.PlayerState.Run:   return StateFilter.Run;
            case PlayerManager.PlayerState.Jump:  return StateFilter.Jump;
            case PlayerManager.PlayerState.InAir: return StateFilter.InAir; 
            case PlayerManager.PlayerState.Punch: return StateFilter.Punch;
            case PlayerManager.PlayerState.Shoot: return StateFilter.Shoot;
        }
    }

    private static FacingFilter ToFacingFilter(RadialCursor8Way.Facing8 f)
    {
        switch (f)
        {
            case RadialCursor8Way.Facing8.Nord:      return FacingFilter.Nord;
            case RadialCursor8Way.Facing8.NordEst:   return FacingFilter.NordEst;
            case RadialCursor8Way.Facing8.Est:       return FacingFilter.Est;
            case RadialCursor8Way.Facing8.SudEst:    return FacingFilter.SudEst;
            case RadialCursor8Way.Facing8.Sud:       return FacingFilter.Sud;
            case RadialCursor8Way.Facing8.SudOuest:  return FacingFilter.SudOuest;
            case RadialCursor8Way.Facing8.Ouest:     return FacingFilter.Ouest;
            case RadialCursor8Way.Facing8.NordOuest: return FacingFilter.NordOuest;
            default:                                  return FacingFilter.Nord;
        }
    }

    private static MoveFilter ToMoveFilter(RadialCursor8Way.MoveDir8 m)
    {
        switch (m)
        {
            case RadialCursor8Way.MoveDir8.Right:     return MoveFilter.Right;
            case RadialCursor8Way.MoveDir8.UpRight:   return MoveFilter.UpRight;
            case RadialCursor8Way.MoveDir8.Up:        return MoveFilter.Up;
            case RadialCursor8Way.MoveDir8.UpLeft:    return MoveFilter.UpLeft;
            case RadialCursor8Way.MoveDir8.Left:      return MoveFilter.Left;
            case RadialCursor8Way.MoveDir8.DownLeft:  return MoveFilter.DownLeft;
            case RadialCursor8Way.MoveDir8.Down:      return MoveFilter.Down;
            case RadialCursor8Way.MoveDir8.DownRight: return MoveFilter.DownRight;
            default:                                   return MoveFilter.Still;
        }
    }
    

    private void SelectRuleIfNeeded()
    {
        int bestIndex = -1;
        int bestScore = int.MinValue;

        for (int i = 0; i < (rules?.Length ?? 0); i++)
        {
            var r = rules[i];
            if (r == null || r.clip == null || !r.clip.IsValid) continue;

            int score = MatchScore(r, _curState, _curFacing, _curMove);
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
            else if (score == bestScore && bestIndex != -1)
            {
                if (r.priority > rules[bestIndex].priority)
                    bestIndex = i;
                else if (r.priority == rules[bestIndex].priority)
                {
                    bestIndex = i;
                }
            }
        }

        if (bestIndex != _lastRuleIndex)
        {
            ApplyRule(bestIndex);
        }
    }
    
    private int MatchScore(AnimRule r, StateFilter st, FacingFilter fa, MoveFilter mo)
    {
        int score = 0;
        
        if (r.state != StateFilter.Any)
        {
            if (r.state != st) return int.MinValue;
            score++;
        }
        
        if (r.facing != FacingFilter.Any)
        {
            if (r.facing != fa) return int.MinValue;
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
            if (_currentClip.randomStartFrame)
                _frameIndex = UnityEngine.Random.Range(0, _currentClip.frames.Length);
            else
                _frameIndex = 0;

            targetRenderer.sprite = _currentClip.frames[_frameIndex];

           /* if (debugLog)
            {
                Debug.Log($"[PlayerSpriteAnimator2D] Rule {ruleIndex} -> clip '{_currentClip.name}' " +
                          $"(State={_curState}, Facing={_curFacing}, Move={_curMove})");
            }*/
        }
    }

    public void ForceReselect(bool resetFrame)
    {
        if (resetFrame)
        {
            _lastRuleIndex = -1; 
        }
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

           /* if (debugLog)
                Debug.Log($"[PlayerSpriteAnimator2D] frame={_frameIndex}");*/
        }
    }
}
