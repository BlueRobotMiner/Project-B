// MonsterAI
// Three moods:
//   Stalk: creeps toward the player in short bursts, pausing to stand and watch.
//   Chase: once it sees the player, it chases. It's a little slower than a player sprint,
//          so the player can outrun it, but it catches up when they walk or stop.
//   Hunt:  goes to the last place it knew the player was, searches nearby, then goes back to Stalk.
//          Hunting starts when the player gets too far away (by walking distance), or stays out of
//          sight too long, and also right after a flash retreat ends (at the spot they were flashed).
// A flash freezes it for a hit reaction, turns it away, and makes it run far away (by walking
// distance). Safe rooms with the lights on make it wait at the Stare Point.
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class MonsterAI : MonoBehaviour
{
    private enum Mood { Stalk, Chase, Hunt }

    [Header("Speeds (match the walk and run clips)")]
    [SerializeField] private float stalkSpeed = 1.2f;     // Slow creep while stalking
    [SerializeField] private float walkSpeed = 1.5f;      // Waiting at a safe room's Stare Point
    [SerializeField] private float chaseSpeed = 3.8f;     // Player sprints at 4.5, walks at 2.5
    [SerializeField] private float huntSpeed = 2.4f;      // Walking pace while searching
    [SerializeField] private float runSpeed = 3.5f;       // Full run speed, used to scale the animation

    [Header("Stalking")]
    [SerializeField] private float stalkMoveMin = 2f;
    [SerializeField] private float stalkMoveMax = 4f;
    [SerializeField] private float stalkPauseMin = 3f;
    [SerializeField] private float stalkPauseMax = 7f;

    [Header("Sight")]
    [SerializeField] private float sightRange = 25f;      // How far it can see
    [SerializeField] private float sightAngle = 140f;     // Field of view, in degrees
    [SerializeField] private float eyeHeight = 1.6f;

    [Header("Chase and hunt")]
    [SerializeField] private float loseDistance = 40f;    // Walking distance at which it stops chasing
    [SerializeField] private float loseSightSeconds = 6f; // Seconds out of sight before it starts hunting
    [SerializeField] private float huntSeconds = 60f;     // How long it searches
    [SerializeField] private float searchRadius = 8f;     // How far around the last known spot it searches

    [Header("Attack")]
    [SerializeField] private float attackDistance = 1.5f;
    [SerializeField] private float attackInterval = 1.5f;
    [SerializeField] private float turnSpeed = 6f;

    [Header("Retreat")]
    [SerializeField] private float retreatDistance = 30f; // Walking distance it runs when flashed
    [SerializeField] private float stuckSeconds = 2f;     // Seconds without moving before it picks a new spot

    [Header("Hit reaction")]
    [SerializeField] private float hitStunSeconds = 1.2f; // Frozen in place while gethit1 plays
    [SerializeField] private float hitTurnSpeed = 10f;    // How fast it turns to face away from the player

    [Header("Animation")]
    [SerializeField] private Animator animator;           // Leave empty to find it on the model

    private NavMeshAgent agent;
    private Transform player;
    private PlayerLife playerLife;

    private Mood mood = Mood.Stalk;
    private Vector3 lastSeen;
    private float lostTimer;
    private float huntTimer;
    private float searchPauseTimer;
    private float attackCooldown;
    private float retreatTimer;
    private float stunTimer;
    private float stuckTimer;
    private Vector3 pendingRetreat;
    private bool hasPendingRetreat;
    private float phaseTimer;
    private bool pausing;
    private float pathCheckTimer;
    private float pathDistance = float.MaxValue;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();

        FirstPersonController fpc = FindObjectOfType<FirstPersonController>();
        if (fpc != null)
        {
            player = fpc.transform;
            playerLife = fpc.GetComponent<PlayerLife>();
        }
    }

    private void Update()
    {
        if (player == null) return;

        Vector3 playerPos = player.position;
        AudioManager.Instance?.UpdateMonsterGrowl(transform.position, playerPos);

        attackCooldown -= Time.deltaTime;

        // Walk and run animations: running only when it's actually moving fast on a chase or retreat
        bool moving = agent.velocity.sqrMagnitude > 0.05f;
        bool running = moving && (mood == Mood.Chase || retreatTimer > 0f);
        animator?.SetFloat("Speed", Mathf.Clamp01(agent.velocity.magnitude / runSpeed), 0.2f, Time.deltaTime);
        animator?.SetBool("Running", running);

        // Hit reaction and running away after a flash
        if (retreatTimer > 0f)
        {
            RunRetreat();
            return;
        }

        // Walking distance, checked a few times a second
        pathCheckTimer -= Time.deltaTime;
        if (pathCheckTimer <= 0f)
        {
            pathDistance = PathDistance(transform.position, playerPos);
            pathCheckTimer = 0.5f;
        }

        // Safe room with the lights on: wait outside and watch
        SafeRoom room = SafeRoom.Containing(playerPos);
        if (room != null && room.LightsOn && room.StarePoint != null)
        {
            WaitAtStarePoint(room, playerPos);
            return;
        }

        // Seeing the player (and being close enough) starts a chase
        bool canSee = CanSeePlayer();
        if (canSee)
        {
            lastSeen = playerPos;
            if (pathDistance <= loseDistance)
                mood = Mood.Chase;
        }

        switch (mood)
        {
            case Mood.Chase:
                ChaseMood(canSee, playerPos);
                break;
            case Mood.Hunt:
                HuntMood();
                break;
            default:
                Stalk(playerPos);
                break;
        }
    }

    // ---- Stalk: creep toward the player, then stand still and watch, over and over ----

    private void Stalk(Vector3 playerPos)
    {
        phaseTimer -= Time.deltaTime;
        if (phaseTimer <= 0f)
        {
            pausing = !pausing;
            phaseTimer = pausing
                ? Random.Range(stalkPauseMin, stalkPauseMax)
                : Random.Range(stalkMoveMin, stalkMoveMax);
        }

        agent.speed = stalkSpeed;

        if (pausing)
        {
            agent.isStopped = true;
            FaceTarget(playerPos);
        }
        else
        {
            agent.isStopped = false;
            agent.SetDestination(playerPos);
        }
    }

    // ---- Chase: follow the player while they're in sight, or to the last spot they were seen ----

    private void ChaseMood(bool canSee, Vector3 playerPos)
    {
        lostTimer = canSee ? 0f : lostTimer + Time.deltaTime;

        if (pathDistance > loseDistance || lostTimer > loseSightSeconds)
        {
            StartHunt();
            return;
        }

        pausing = false;
        agent.speed = chaseSpeed;
        agent.isStopped = false;
        agent.SetDestination(canSee ? playerPos : lastSeen);

        if (canSee && Vector3.Distance(transform.position, playerPos) <= attackDistance && attackCooldown <= 0f)
        {
            animator?.SetTrigger("Attack");
            attackCooldown = attackInterval;
            playerLife?.TakeHit();
        }
    }

    // ---- Hunt: go to the last known spot, then search and lurk nearby ----

    private void StartHunt()
    {
        mood = Mood.Hunt;
        huntTimer = huntSeconds;
        searchPauseTimer = 0f;
        agent.speed = huntSpeed;
        agent.isStopped = false;
        agent.SetDestination(lastSeen);
    }

    private void HuntMood()
    {
        huntTimer -= Time.deltaTime;
        if (huntTimer <= 0f)
        {
            mood = Mood.Stalk;
            return;
        }

        agent.speed = huntSpeed;

        bool arrived = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f;
        if (!arrived)
        {
            agent.isStopped = false;
            return;
        }

        // Arrived: stand still for a moment, then search a new spot near the last known position
        agent.isStopped = true;
        searchPauseTimer -= Time.deltaTime;
        if (searchPauseTimer <= 0f)
        {
            searchPauseTimer = Random.Range(stalkPauseMin, stalkPauseMax);
            agent.isStopped = false;
            agent.SetDestination(RandomPointNear(lastSeen, searchRadius));
        }
    }

    // ---- Safe room ----

    private void WaitAtStarePoint(SafeRoom room, Vector3 playerPos)
    {
        pausing = false;
        agent.speed = walkSpeed;
        agent.isStopped = false;
        agent.SetDestination(room.StarePoint.position);

        if (Vector3.Distance(transform.position, room.StarePoint.position) < 1f)
        {
            agent.isStopped = true;
            FaceTarget(playerPos);
        }
    }

    // ---- Flash: hit reaction, turn away, run away, then hunt the spot they were flashed at ----

    // Called by FlashDevice when the flash hits this monster.
    public void Retreat(float seconds)
    {
        retreatTimer = Mathf.Max(retreatTimer, seconds);

        // The player's position at the moment of the flash is where the hunt will start
        lastSeen = player != null ? player.position : transform.position;

        // Freeze for the hit reaction, then run to the chosen spot
        stunTimer = hitStunSeconds;
        pendingRetreat = FindRetreatPoint();
        hasPendingRetreat = true;
        stuckTimer = 0f;

        agent.isStopped = true;
        PlayHitAnimation();
    }

    private void RunRetreat()
    {
        retreatTimer -= Time.deltaTime;

        // Hit reaction: stay frozen and turn to face away from the player
        if (stunTimer > 0f)
        {
            stunTimer -= Time.deltaTime;
            agent.isStopped = true;
            FaceAwayFromPlayer();

            if (retreatTimer <= 0f) StartHunt();
            return;
        }

        // The stun is over: start running to the retreat spot
        if (hasPendingRetreat)
        {
            hasPendingRetreat = false;
            agent.speed = chaseSpeed;
            agent.isStopped = false;
            agent.SetDestination(pendingRetreat);
        }

        bool arrived = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f;

        if (arrived)
        {
            // Reached the spot: stay put for the rest of the retreat
            agent.isStopped = true;
            stuckTimer = 0f;
        }
        else if (!agent.isStopped && !agent.pathPending && agent.velocity.sqrMagnitude < 0.02f)
        {
            // Trying to run but not moving (stuck on a corner): pick a new spot
            stuckTimer += Time.deltaTime;
            if (stuckTimer >= stuckSeconds)
            {
                stuckTimer = 0f;
                agent.SetDestination(FindRetreatPoint());
            }
        }
        else
        {
            stuckTimer = 0f;
        }

        // Retreat is over: go to the spot where the player was flashed, and search there
        if (retreatTimer <= 0f)
            StartHunt();
    }

    // Plays the gethit1 state directly, with the Hit trigger as a fallback.
    private void PlayHitAnimation()
    {
        if (animator == null) return;

        if (animator.HasState(0, Animator.StringToHash("gethit1")))
        {
            animator.CrossFadeInFixedTime("gethit1", 0.1f);
        }
        else
        {
            animator.SetTrigger("Hit");
            Debug.LogWarning("MonsterAI: no 'gethit1' state found, so the Hit trigger was used instead.", this);
        }
    }

    // Turns the monster to face directly away from the player.
    private void FaceAwayFromPlayer()
    {
        if (player == null) return;

        Vector3 away = transform.position - player.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.001f) return;

        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(away), hitTurnSpeed * Time.deltaTime);
    }

    // Finds a NavMesh spot whose walking distance from the player is about retreatDistance.
    private Vector3 FindRetreatPoint()
    {
        Vector3 from = player != null ? player.position : transform.position;

        Vector3 bestPoint = transform.position;
        float bestGap = float.MaxValue;

        for (int i = 0; i < 25; i++)
        {
            Vector2 circle = Random.insideUnitCircle.normalized;
            float straight = retreatDistance * Random.Range(0.3f, 1.5f);
            Vector3 candidate = from + new Vector3(circle.x, 0f, circle.y) * straight;

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                continue;

            float walking = PathDistance(hit.position, from);
            if (walking == float.MaxValue || walking < retreatDistance * 0.9f)
                continue;

            float gap = Mathf.Abs(walking - retreatDistance);
            if (gap < bestGap)
            {
                bestGap = gap;
                bestPoint = hit.position;
            }
        }

        return bestPoint;
    }

    // ---- Sight and helpers ----

    // True when the player is inside the field of view and nothing solid is between them.
    private bool CanSeePlayer()
    {
        if (player == null) return false;

        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 target = player.position + Vector3.up * 1f;
        Vector3 toPlayer = target - eye;
        float distance = toPlayer.magnitude;

        if (distance > sightRange || distance < 0.01f) return false;
        if (Vector3.Angle(transform.forward, toPlayer) > sightAngle * 0.5f) return false;

        RaycastHit[] hits = Physics.RaycastAll(eye, toPlayer / distance, distance, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.transform.IsChildOf(transform)) continue;   // The monster itself
            return hit.transform.IsChildOf(player);             // The first thing hit must be the player
        }

        return false;
    }

    private Vector3 RandomPointNear(Vector3 center, float radius)
    {
        Vector2 offset = Random.insideUnitCircle * radius;
        Vector3 candidate = center + new Vector3(offset.x, 0f, offset.y);
        return NavMesh.SamplePosition(candidate, out NavMeshHit hit, 4f, NavMesh.AllAreas) ? hit.position : center;
    }

    // Walking distance along the NavMesh. Returns a huge number if there's no path.
    private float PathDistance(Vector3 from, Vector3 to)
    {
        NavMeshPath path = new NavMeshPath();
        if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
            return float.MaxValue;

        float total = 0f;
        for (int i = 1; i < path.corners.Length; i++)
            total += Vector3.Distance(path.corners[i - 1], path.corners[i]);

        return total;
    }

    private void FaceTarget(Vector3 target)
    {
        Vector3 direction = target - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), turnSpeed * Time.deltaTime);
    }
}
