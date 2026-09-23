using System.Collections.Generic;
using UnityEngine;
using Tile = TileGrid.Tile;

/// <summary>
/// Pszczoły: swobodny lot po całym habitacie, w którym powstały (pozycja skryptem, skrzydła z Animatora).
/// Wybiera losowy punkt nad losowym kafelkiem habitatu, leci do niego z płynnym sterowaniem, potem losuje kolejny.
/// </summary>
[DisallowMultipleComponent]
public class HabitatBeeWanderFlight : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private int animatorLayer;
    [SerializeField] private string wingFlapStateName = "Fly";
    [SerializeField, Min(0.1f), Tooltip("Mnożnik prędkości animacji skrzydeł.")]
    private float wingFlapSpeed = 5f;

    [Header("Lot — obszar")]
    [SerializeField, Min(0f)] private float flyHeight = 1.35f;
    [SerializeField, Range(0.1f, 0.5f), Tooltip("Promień wędrówki nad kafelkiem jako ułamek odległości między środkami kafli.")]
    private float wanderRadiusFraction = 0.4f;
    [SerializeField, Min(0.1f), Tooltip("Odległość między środkami kafli, gdy habitat ma tylko jeden kafel.")]
    private float fallbackTileSpacing = 8.66f;

    [Header("Lot — ruch")]
    [SerializeField, Min(0.1f)] private float cruiseSpeed = 3.2f;
    [SerializeField, Min(0.1f)] private float acceleration = 6f;
    [SerializeField, Min(0.05f)] private float arriveDistance = 0.6f;
    [SerializeField, Min(0f)] private float verticalBobAmplitude = 0.12f;
    [SerializeField, Min(0.01f)] private float verticalBobFrequency = 2.4f;
    [SerializeField, Min(0f)] private float faceMotionSmoothing = 8f;
    [SerializeField, Range(0f, 1f)] private float tiltTowardMotion = 0.25f;

    private readonly List<Vector3> _tileCenters = new();
    private Vector3 _anchor;
    private float _wanderRadius;
    private Vector3 _flatPos;
    private Vector3 _velocity;
    private Vector3 _target;
    private float _tileSpacing;
    private int _currentTile;
    private readonly List<List<int>> _adjacent = new();
    private readonly List<Vector3> _route = new();
    private int _routeIndex;
    private float _bobPhase;
    private bool _initialized;
    private int _wingHash;

    private void Awake()
    {
        if (!animator)
            animator = GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        if (animator != null)
        {
            animator.enabled = true;
            animator.speed = wingFlapSpeed;
        }

        ResolveWingStateNameIfNeeded();
        PlayWingAnimation(randomOffset: true);
    }

    public void Initialize(Vector3 anchorWorld, IReadOnlyList<Tile> habitatTiles)
    {
        _anchor = anchorWorld;

        _tileCenters.Clear();
        if (habitatTiles != null)
        {
            for (int i = 0; i < habitatTiles.Count; i++)
            {
                if (habitatTiles[i] != null)
                    _tileCenters.Add(habitatTiles[i].worldPos);
            }
        }
        if (_tileCenters.Count == 0)
            _tileCenters.Add(anchorWorld);

        _tileSpacing = ComputeTileSpacing();
        _wanderRadius = _tileSpacing * wanderRadiusFraction;
        BuildAdjacency();
        _flatPos = FlatPoint(anchorWorld);
        _currentTile = NearestTile(_flatPos);
        _velocity = Vector3.zero;
        _bobPhase = Random.Range(0f, Mathf.PI * 2f);
        BuildRoute();
        _initialized = true;

        transform.position = WithHeight(_flatPos);
        var toTarget = _target - _flatPos;
        if (toTarget.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
    }

    private void Update()
    {
        KeepWingsFlapping();

        if (!_initialized)
            return;

        float dt = Time.deltaTime;

        Vector3 toTarget = _target - _flatPos;
        bool lastWaypoint = _routeIndex >= _route.Count - 1;
        if (toTarget.magnitude <= (lastWaypoint ? arriveDistance : arriveDistance * 2f))
        {
            _currentTile = NearestTile(_flatPos);
            if (lastWaypoint)
                BuildRoute();
            else
                _target = _route[++_routeIndex];
            toTarget = _target - _flatPos;
        }

        Vector3 desired = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized * cruiseSpeed : Vector3.zero;
        _velocity = Vector3.MoveTowards(_velocity, desired, acceleration * dt);
        _flatPos += _velocity * dt;

        _bobPhase += verticalBobFrequency * dt;
        float bob = Mathf.Sin(_bobPhase) * verticalBobAmplitude;
        transform.position = WithHeight(_flatPos, bob);

        Vector3 facing = _velocity;
        facing.y = _velocity.magnitude * -tiltTowardMotion * 0.2f;
        if (_velocity.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(facing.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, faceMotionSmoothing * dt);
        }
    }

    /// <summary>
    /// Picks a random destination tile and routes there tile-by-tile through adjacent habitat tiles
    /// only (BFS), so a non-compact habitat never sends the bee across a tile that isn't part of it.
    /// Transit waypoints stay near tile centers so each leg remains inside the two tiles it joins.
    /// </summary>
    private void BuildRoute()
    {
        _route.Clear();
        _routeIndex = 0;

        int count = _tileCenters.Count;
        int dest = _currentTile;
        if (count > 1)
        {
            dest = Random.Range(0, count - 1);
            if (dest >= _currentTile) dest++;
        }

        List<int> path = FindPath(_currentTile, dest);
        float transitJitter = _tileSpacing * 0.2f;

        if (path != null && path.Count > 1)
        {
            for (int i = 1; i < path.Count - 1; i++)
                _route.Add(JitteredPoint(_tileCenters[path[i]], transitJitter));

            _route.Add(JitteredPoint(_tileCenters[dest], Mathf.Min(_wanderRadius, _tileSpacing * 0.25f)));
        }
        else
        {
            // Same tile (single-tile habitat) or unreachable: roam within the current tile only.
            _route.Add(JitteredPoint(_tileCenters[_currentTile], _wanderRadius));
        }

        _target = _route[0];
    }

    private static Vector3 JitteredPoint(Vector3 center, float radius)
    {
        Vector2 disc = Random.insideUnitCircle * radius;
        return new Vector3(center.x + disc.x, 0f, center.z + disc.y);
    }

    private void BuildAdjacency()
    {
        _adjacent.Clear();
        float maxLink = _tileSpacing * 1.15f;

        for (int i = 0; i < _tileCenters.Count; i++)
            _adjacent.Add(new List<int>(6));

        for (int i = 0; i < _tileCenters.Count; i++)
        {
            for (int j = i + 1; j < _tileCenters.Count; j++)
            {
                if ((FlatPoint(_tileCenters[i]) - FlatPoint(_tileCenters[j])).magnitude <= maxLink)
                {
                    _adjacent[i].Add(j);
                    _adjacent[j].Add(i);
                }
            }
        }
    }

    private int NearestTile(Vector3 flat)
    {
        int best = 0;
        float bestDist = float.MaxValue;
        for (int i = 0; i < _tileCenters.Count; i++)
        {
            float d = (FlatPoint(_tileCenters[i]) - flat).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = i;
            }
        }
        return best;
    }

    private List<int> FindPath(int from, int to)
    {
        if (from == to)
            return null;

        int n = _tileCenters.Count;
        var prev = new int[n];
        for (int i = 0; i < n; i++) prev[i] = -1;
        prev[from] = from;

        var queue = new Queue<int>();
        queue.Enqueue(from);

        while (queue.Count > 0)
        {
            int cur = queue.Dequeue();
            if (cur == to)
                break;

            foreach (int next in _adjacent[cur])
            {
                if (prev[next] != -1) continue;
                prev[next] = cur;
                queue.Enqueue(next);
            }
        }

        if (prev[to] == -1)
            return null;

        var path = new List<int>();
        for (int at = to; at != from; at = prev[at])
            path.Add(at);
        path.Add(from);
        path.Reverse();
        return path;
    }

    private float ComputeTileSpacing()
    {
        if (_tileCenters.Count < 2)
            return fallbackTileSpacing;

        float nearest = float.MaxValue;
        for (int i = 0; i < _tileCenters.Count; i++)
        {
            for (int j = i + 1; j < _tileCenters.Count; j++)
            {
                float d = (FlatPoint(_tileCenters[i]) - FlatPoint(_tileCenters[j])).magnitude;
                if (d > 0.001f && d < nearest)
                    nearest = d;
            }
        }

        return nearest < float.MaxValue ? nearest : fallbackTileSpacing;
    }

    private static Vector3 FlatPoint(Vector3 p) => new Vector3(p.x, 0f, p.z);

    private Vector3 WithHeight(Vector3 flat, float bob = 0f)
        => new Vector3(flat.x, _anchor.y + flyHeight + bob, flat.z);

    // -------------------------------------------------------------------------
    // Wing animation
    // -------------------------------------------------------------------------

    private void ResolveWingStateNameIfNeeded()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        if (!string.IsNullOrEmpty(wingFlapStateName) && HasState(wingFlapStateName))
        {
            _wingHash = Animator.StringToHash(wingFlapStateName);
            return;
        }

        if (HasState("Fly"))
        {
            wingFlapStateName = "Fly";
            _wingHash = Animator.StringToHash(wingFlapStateName);
            return;
        }

        var clips = animator.runtimeAnimatorController.animationClips;
        if (clips == null || clips.Length == 0)
            return;

        for (int i = 0; i < clips.Length; i++)
        {
            var clip = clips[i];
            if (clip == null)
                continue;

            string name = clip.name;
            if (name.IndexOf("fly", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("wing", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("idle", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                wingFlapStateName = name;
                _wingHash = Animator.StringToHash(name);
                return;
            }
        }

        wingFlapStateName = clips[0].name;
        _wingHash = Animator.StringToHash(wingFlapStateName);
    }

    private bool HasState(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
            return false;

        return animator.HasState(animatorLayer, Animator.StringToHash(stateName));
    }

    private void PlayWingAnimation(bool randomOffset)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        float offset = randomOffset ? Random.value : 0f;

        if (_wingHash != 0 && animator.HasState(animatorLayer, _wingHash))
            animator.Play(_wingHash, animatorLayer, offset);
        else
            animator.Play(0, animatorLayer, offset);
    }

    /// <summary>
    /// The imported clip is non-looping by default, so the Animator plays it once and holds the last
    /// frame. Restart it whenever a non-looping clip finishes so the wings flap forever regardless
    /// of the clip's Loop Time import flag.
    /// </summary>
    private void KeepWingsFlapping()
    {
        if (animator == null || !animator.enabled || animator.runtimeAnimatorController == null)
            return;

        var info = animator.GetCurrentAnimatorStateInfo(animatorLayer);
        if (!info.loop && info.normalizedTime >= 1f)
            PlayWingAnimation(randomOffset: false);
    }
}
