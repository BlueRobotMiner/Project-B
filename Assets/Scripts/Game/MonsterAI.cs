// MonsterAI
// Moods:
//   Stalk:  always creeps toward the player along the NavMesh. Freezes while the player looks at it.
//   Stare:  close enough to watch from around a corner. Stays frozen and faces the player.
//           When the player looks away (or after a while), it either ambushes (chase) or vanishes.
//   Vanish: runs to a spot out of the player's view, then goes back to stalking.
//   Chase:  sprints at the player while it can. Slightly slower than a sprint, so it can be outrun.
//   Hunt:   goes to the last known position (coordinates), searches around it, then back to Stalk.
// Hearing: sprinting is heard from farther away than walking. Hearing starts a hunt at the sound.
// Flash (retreat): freeze for the hit reaction, turn straight away from the player, run away
// (by walking distance), then hunt the spot where the player was flashed. Every phase has a
// timeout, so it always moves on.
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class MonsterAI : MonoBehaviour
{
    private enum Mood { Stalk, Stare, Vanish, Chase, Hunt }
    private enum RetreatPhase { Stunned, Turning, Running }

    [Header("Speeds (match the walk and run clips)")]
    [SerializeField] private float stalkSpeed = 1.2f;     // Slow creep while stalking
    [SerializeField] private float walkSpeed = 1.5f;      // Waiting at a safe room's Stare Point
    [SerializeField] private float chaseSpeed = 3.8f;     // Player sprints at 4.5, walks at 2.5
    [SerializeField] private float huntSpeed = 2.4f;      // Walking pace while searching
    [SerializeField] private float runSpeed = 3.5f;       // Vanishing, retreating, and animation scale

    [Header("Stare and ambush")]
    [SerializeField] private float stareRange = 10f;      // Walking distance at which it stops and stares
    [SerializeField] private float stareSeconds = 6f;     // How long it stares if the player never looks
    [SerializeField] private float ambushChance = 0.4f;   // Chance it charges instead of vanishing

    [Header("Watching (the player's view)")]
    [SerializeField] private float watchRange = 30f;      // How far the player's view can freeze it
    [SerializeField] private float watchAngle = 45f;      // Half-angle of the player's view

    [Header("Hearing")]
    [SerializeField] private float walkHearRange = 10f;   // Walking distance it hears walking
    [SerializeField] private float sprintHearRange = 25f; // Walking distance it hears sprinting
    [SerializeField] private float walkSpeedThreshold = 1.5f;
    [SerializeField] private float sprintSpeedThreshold = 3.5f;

    [Header("Vanish")]
    [SerializeField] private float vanishDistance = 25f;  // Walking distance it runs to when vanishing
    [SerializeField] private float vanishSeconds = 15f;   // Gives up on a vanish after this long

    [Header("Sight")]
    [SerializeField] private float sightRange = 25f;      // How far it can see
    [SerializeField] private float sightAngle = 140f;     // Field of view, in degrees
    [SerializeField] private float eyeHeight = 1.6f;

    [Header("Chase and hunt")]
    [SerializeField] private float loseDistance = 40f;    // Walking distance at which it stops chasing
    [SerializeField] private float loseSightSeconds = 6f; // Seconds out of sight before it starts hunting
    [SerializeField] private float huntSeconds = 60f;     // How long it searches
    [SerializeField] private float searchRadius = 8f;     // How far around the last known position it searches

    [Header("Attack")]
    [SerializeField] private float attackDistance = 1.5f;
    [SerializeField] private float attackInterval = 1.5f;
    [SerializeField] private float turnSpeed = 6f;

    [Header("Retreat (flash)")]
    [SerializeField] private float retreatDistance = 30f; // Walking distance it runs when flashed
    [SerializeField] private float stuckSeconds = 2f;     // Seconds without moving before it picks a new spot
    [SerializeField] private float turnTimeout = 2f;      // Longest it will spend turning away before running
    [SerializeField] private float hitStunSeconds = 1.2f; // Frozen while gethit1 plays. Set to the clip length.
    [SerializeField] private float hitTurnSpeed = 240f;   // Degrees per second while turning away

    [Header("Animation")]
    [SerializeField] private Animator animator;           // Leave empty to find it on the model
    [SerializeField] private float maxFreezeSeconds = 4f;   // Longest it stays frozen in one watch before creeping again
    private float freezeTimer;
    [SerializeField] private float freezeBreakDistance = 6f;   // If the player gets this close, it stops freezing and hunts them
    [SerializeField] private float freezeLogInterval = 2f;     // Seconds between debug messages while it's frozen or staring
    private float freezeLogTimer;
    [SerializeField] private float retreatPassMargin = 3f;     // Routes passing this close to the player are rejected
    [SerializeField] private float retreatReplanSeconds = 1.5f; // How often it re-plans while you're close
    private float retreatReplanTimer;




    private NavMeshAgent agent;
    private Transform player;
    private PlayerLife playerLife;
    private CharacterController playerController;
    private Camera viewCamera;

    private Mood mood = Mood.Stalk;
    private Vector3 lastSeen;
    private float lostTimer;
    private float huntTimer;
    private float searchPauseTimer;
    private float attackCooldown;
    private float pathCheckTimer;
    private float pathDistance = float.MaxValue;
    private float stareTimer;
    private bool wasWatched;
    private float vanishTimer;

    private float retreatTimer;
    private RetreatPhase retreatPhase;
    private float retreatPhaseTimer;
    private float stuckTimer;
    private Vector3 pendingRetreat;

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
            playerController = fpc.GetComponent<CharacterController>();
        }

        // The camera the player looks through. Falls back to the main camera.
        viewCamera = player != null ? player.GetComponentInChildren<Camera>() : null;
        if (viewCamera == null) viewCamera = Camera.main;
    }

    private void Update()
    {
        if (player == null) return;

        Vector3 playerPos = player.position;
        AudioManager.Instance?.UpdateMonsterGrowl(transform.position, playerPos);

        attackCooldown -= Time.deltaTime;

        // Animation: running only while it's moving fast on a chase, a vanish, or a retreat
        bool moving = agent.velocity.sqrMagnitude > 0.05f;
        bool running = moving && (mood == Mood.Chase || mood == Mood.Vanish || retreatTimer > 0f);
        animator?.SetFloat("Speed", Mathf.Clamp01(agent.velocity.magnitude / runSpeed), 0.2f, Time.deltaTime);
        animator?.SetBool("Running", running);

        // Flash: freeze, turn away, run, then hunt the flash spot
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

        bool watched = IsWatchedByPlayer();
        bool canSee = CanSeePlayer();
        if (Time.frameCount % 60 == 0)
        {
            Debug.Log($"Monster: mood={mood} watched={watched} stopped={agent.isStopped} hasPath={agent.hasPath} pathDist={pathDistance:F0} vel={agent.velocity.magnitude:F2}");
        }

        // Hearing: it turns toward any sound it can hear, even from behind
        if (mood != Mood.Chase && mood != Mood.Vanish && HearsPlayer())
        {
            lastSeen = playerPos;
            StartHunt();
        }

        switch (mood)
        {
            case Mood.Stalk:
                StalkMood(watched, playerPos);
                break;
            case Mood.Stare:
                StareMood(watched, playerPos);
                break;
            case Mood.Vanish:
                VanishMood();
                break;
            case Mood.Chase:
                ChaseMood(canSee, playerPos);
                break;
            case Mood.Hunt:
                if (canSee && !watched && pathDistance <= loseDistance)
                {
                    lastSeen = playerPos;
                    mood = Mood.Chase;
                    ChaseMood(true, playerPos);
                }
                else
                {
                    HuntMood();
                }
                break;
        }
    }

    // ---- Stalk: always creeps toward the player. Freezes while they look at it. ----

    private void StalkMood(bool watched, Vector3 playerPos)
    {
        // Player got close: stop freezing and hunt them right away
        if (PlayerTooClose(playerPos))
        {
            BreakFreeze(playerPos);
            return;
        }

        if (watched)
        {
            freezeTimer += Time.deltaTime;
        }
        else
        {
            freezeTimer = 0f;
        }

        // Frozen while watched, but only up to maxFreezeSeconds at a time
        if (watched && freezeTimer < maxFreezeSeconds)
        {
            LogFreezeState("frozen", freezeTimer, maxFreezeSeconds, watched);
            agent.isStopped = true;
            FaceTarget(playerPos);
            return;
        }

        // Close enough: stop around the corner and stare
        if (pathDistance <= stareRange)
        {
            EnterStare();
            return;
        }

        agent.speed = stalkSpeed;
        agent.isStopped = false;
        SetDestinationSafe(playerPos);
    }

    // ---- Stare: stand still, face the player, and wait for them to look away ----

    private void EnterStare()
    {
        mood = Mood.Stare;
        stareTimer = stareSeconds;
        wasWatched = false;
        agent.isStopped = true;
        }
        private bool PlayerTooClose(Vector3 playerPos)
    {
        return Vector3.Distance(transform.position, playerPos) <= freezeBreakDistance;
    }

    // Ends any freeze or stare and starts chasing the player immediately
    private void BreakFreeze(Vector3 playerPos)
    {
        freezeTimer = 0f;
        lastSeen = playerPos;
        lostTimer = 0f;
        mood = Mood.Chase;
        agent.isStopped = false;
        Debug.Log("Monster: the player is too close. Breaking the freeze and hunting.", this);
    }

    // Prints how long it has been frozen or staring, every freezeLogInterval seconds
    private void LogFreezeState(string label, float timer, float limit, bool watched)
    {
        freezeLogTimer -= Time.deltaTime;
        if (freezeLogTimer > 0f) return;

        freezeLogTimer = freezeLogInterval;
        Debug.Log($"Monster {label}: {timer:F1}s of {limit:F1}s (watched={watched})", this);
    }


        private void StareMood(bool watched, Vector3 playerPos)
    {
        // Player got close: stop staring and hunt them right away
        if (PlayerTooClose(playerPos))
        {
            BreakFreeze(playerPos);
            return;
        }

        agent.isStopped = true;
        FaceTarget(playerPos);

        if (watched)
        {
            wasWatched = true;   // Frozen while the player is looking
            LogFreezeState("staring (watched)", stareTimer, stareSeconds, watched);
            return;
        }

        stareTimer -= Time.deltaTime;
        LogFreezeState("staring", stareTimer, stareSeconds, watched);

        // The player looked away, or waited too long: ambush or vanish
        if (wasWatched || stareTimer <= 0f)
        {
            if (Random.value < ambushChance)
            {
                lastSeen = playerPos;
                mood = Mood.Chase;
            }
            else
            {
                StartVanish();
            }
        }
}

    // ---- Vanish: run to a spot out of the player's view, then go back to stalking ----

    private void StartVanish()
    {
        mood = Mood.Vanish;
        vanishTimer = vanishSeconds;
        agent.speed = runSpeed;
        agent.isStopped = false;

        if (!SetDestinationSafe(FindVanishPoint()))
            mood = Mood.Stalk;
    }

    private void VanishMood()
    {
        vanishTimer -= Time.deltaTime;

        bool arrived = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f;
        if (arrived || vanishTimer <= 0f)
        {
            mood = Mood.Stalk;
            agent.isStopped = false;
        }
    }

    // A walkable spot about vanishDistance away (by walking) that's out of the player's view.
    private Vector3 FindVanishPoint()
    {
        Vector3 from = player != null ? player.position : transform.position;
        Vector3 camPosition = viewCamera != null ? viewCamera.transform.position : from;
        Vector3 camForward = viewCamera != null ? viewCamera.transform.forward : Vector3.forward;

        for (int i = 0; i < 25; i++)
        {
            Vector2 circle = Random.insideUnitCircle.normalized;
            float straight = vanishDistance * Random.Range(0.4f, 1.2f);
            Vector3 candidate = from + new Vector3(circle.x, 0f, circle.y) * straight;

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                continue;

            float walking = PathDistance(hit.position, from);
            if (walking == float.MaxValue || walking < vanishDistance * 0.6f)
                continue;

            // Out of the player's view: outside their cone, or too far to be seen
            Vector3 toSpot = hit.position + Vector3.up - camPosition;
            bool inView = toSpot.magnitude <= watchRange && Vector3.Angle(camForward, toSpot) <= watchAngle + 20f;
            if (!inView)
                return hit.position;
        }

        return FindRetreatPoint();   // Fallback: any reachable spot far away
    }

    // ---- Chase: follow the player while they're in sight, or to the last known position ----

    private void ChaseMood(bool canSee, Vector3 playerPos)
    {
        lostTimer = canSee ? 0f : lostTimer + Time.deltaTime;

        if (pathDistance > loseDistance || lostTimer > loseSightSeconds)
        {
            StartHunt();
            return;
        }

        agent.speed = chaseSpeed;
        agent.isStopped = false;
        SetDestinationSafe(canSee ? playerPos : lastSeen);

        if (canSee && Vector3.Distance(transform.position, playerPos) <= attackDistance && attackCooldown <= 0f)
        {
            animator?.SetTrigger("Attack");
            attackCooldown = attackInterval;
            playerLife?.TakeHit();
        }
    }

    // ---- Hunt: go to the last known position, search the area, then go back to stalking ----

    private void StartHunt()
    {
        mood = Mood.Hunt;
        huntTimer = huntSeconds;
        searchPauseTimer = 0f;
        agent.speed = huntSpeed;
        agent.isStopped = false;

        // If the position can't be reached at all, give up hunting and stalk instead
        if (!SetDestinationSafe(lastSeen))
            mood = Mood.Stalk;
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

        bool arrived = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.6f;
        if (!arrived)
        {
            agent.isStopped = false;
            return;
        }

        // Arrived: stand still for a moment, then search a new spot near the last known position
        agent.isStopped = true;
        searchPauseTimer -= Time.deltaTime;
        if (searchPauseTimer > 0f) return;

        searchPauseTimer = Random.Range(2f, 5f);
        if (!SetDestinationSafe(RandomPointNear(lastSeen, searchRadius)))
        {
            mood = Mood.Stalk;   // Nowhere reachable to search, so go back to stalking
            return;
        }

        agent.isStopped = false;
    }

    // ---- Safe room ----

    private void WaitAtStarePoint(SafeRoom room, Vector3 playerPos)
    {
        mood = Mood.Stalk;
        agent.speed = walkSpeed;
        agent.isStopped = false;
        agent.SetDestination(room.StarePoint.position);

        if (Vector3.Distance(transform.position, room.StarePoint.position) < 1f)
        {
            agent.isStopped = true;
            FaceTarget(playerPos);
        }
    }

    // ---- Hearing ----

    // Sprinting is heard from farther away than walking. Standing still is not heard.
    private bool HearsPlayer()
    {
        if (playerController == null) return false;

        float speed = playerController.velocity.magnitude;
        float range = speed >= sprintSpeedThreshold ? sprintHearRange
                    : speed >= walkSpeedThreshold ? walkHearRange
                    : 0f;

        return range > 0f && pathDistance <= range;
    }

    // ---- Flash (retreat) ----

    // Called by FlashDevice when the flash hits this monster.
    public void Retreat(float seconds)
    {
        retreatTimer = Mathf.Max(retreatTimer, seconds);

        // The player's position at the moment of the flash is where the hunt will start
        lastSeen = player != null ? player.position : transform.position;

        BeginStun();
    }

    private void BeginStun()
    {
        retreatPhase = RetreatPhase.Stunned;
        retreatPhaseTimer = hitStunSeconds;
        stuckTimer = 0f;

        agent.isStopped = true;
        PlayHitAnimation();
    }

    private void RunRetreat()
    {
        retreatTimer -= Time.deltaTime;
        retreatPhaseTimer -= Time.deltaTime;

        switch (retreatPhase)
        {
            case RetreatPhase.Stunned:
                // Frozen while the hit reaction plays
                agent.isStopped = true;
                if (retreatPhaseTimer <= 0f)
                {
                    retreatPhase = RetreatPhase.Turning;
                    retreatPhaseTimer = turnTimeout;
                }
                break;

            case RetreatPhase.Turning:
                // Turn straight away from the player, then run (or run anyway if it takes too long)
                agent.isStopped = true;
                if (TurnAwayFromPlayer() || retreatPhaseTimer <= 0f)
                    StartRunning();
                break;

            case RetreatPhase.Running:
                UpdateRunning();
                break;
        }

        // Retreat is over: go to the spot where the player was flashed, and search there
        if (retreatTimer <= 0f)
            StartHunt();
    }

    private void StartRunning()
{
    retreatPhase = RetreatPhase.Running;
    agent.speed = runSpeed;
    agent.isStopped = false;
    stuckTimer = 0f;
    retreatReplanTimer = retreatReplanSeconds;

    // Choose the escape point now, using where the player is now
    pendingRetreat = FindRetreatPoint();
    SetDestinationSafe(pendingRetreat);
}

    private void UpdateRunning()
    {
        retreatReplanTimer -= Time.deltaTime;

        Vector3 from = player != null ? player.position : transform.position;
        bool threatened = Vector3.Distance(transform.position, from) < retreatDistance * 0.6f;
        bool hasPath = agent.hasPath || agent.pathPending;

        // While the player is close, re-plan using their current position
        if (retreatReplanTimer <= 0f && (threatened || !hasPath))
        {
            retreatReplanTimer = retreatReplanSeconds;
            pendingRetreat = FindRetreatPoint();
            agent.isStopped = false;
            SetDestinationSafe(pendingRetreat);
            return;
        }

        bool arrived = hasPath && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f;
        if (arrived)
        {
            agent.isStopped = true;
            stuckTimer = 0f;
            return;
        }

        // Trying to move but not moving: pick a new spot
        bool notMoving = !agent.isStopped && agent.velocity.sqrMagnitude < 0.02f;
        if (notMoving)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer >= stuckSeconds)
            {
                stuckTimer = 0f;
                pendingRetreat = FindRetreatPoint();
                agent.isStopped = false;
                SetDestinationSafe(pendingRetreat);
            }
        }
        else
        {
            stuckTimer = 0f;
        }
    }

    // Turns the monster to face directly away from the player. Returns true once it's facing away.
    private bool TurnAwayFromPlayer()
    {
        if (player == null) return true;

        Vector3 away = transform.position - player.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.001f) return true;

        Quaternion goal = Quaternion.LookRotation(away);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, goal, hitTurnSpeed * Time.deltaTime);
        return Quaternion.Angle(transform.rotation, goal) < 10f;
    }

    // A reachable spot about retreatDistance (by walking) from the player, in the direction away from them.
    // Never returns the monster's own position unless there's no NavMesh at all.
        private Vector3 FindRetreatPoint()
    {
        Vector3 me = transform.position;
        Vector3 from = player != null ? player.position : me;

        Vector3 away = me - from;
        away.y = 0f;
        away = away.sqrMagnitude > 0.01f ? away.normalized : transform.forward;

        bool found = false;
        Vector3 best = Vector3.zero;
        float bestScore = float.MaxValue;

        for (int i = 0; i < 40; i++)
        {
            Vector2 circle = Random.insideUnitCircle.normalized;
            Vector3 dir = new Vector3(circle.x, 0f, circle.y);

            // Only spots on the monster's side of the player
            if (Vector3.Dot(away, dir) < 0.3f) continue;

            Vector3 candidate = from + dir * retreatDistance * Random.Range(0.8f, 1.3f);
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas)) continue;

            NavMeshPath monsterPath = new NavMeshPath();
            if (!NavMesh.CalculatePath(me, hit.position, NavMesh.AllAreas, monsterPath)
                || monsterPath.status != NavMeshPathStatus.PathComplete)
                continue;

            // The monster must get there first
            float playerWalk = PathDistance(from, hit.position);
            float monsterWalk = PathLength(monsterPath);
            if (playerWalk == float.MaxValue || monsterWalk >= playerWalk) continue;

            // The monster's route must not pass close to the player
            if (PathPassesNear(monsterPath, from, retreatPassMargin)) continue;

            float score = Mathf.Abs(playerWalk - retreatDistance);
            if (score < bestScore)
            {
                bestScore = score;
                best = hit.position;
                found = true;
            }
        }

        if (found) return best;

        // Fallback: straight away from the player
        Vector3 fallback = me + away * retreatDistance;
        if (NavMesh.SamplePosition(fallback, out NavMeshHit fallbackHit, 4f, NavMesh.AllAreas))
            return fallbackHit.position;

        return me;
    }
    // Total length of a calculated path
    private static float PathLength(NavMeshPath path)
    {
        float total = 0f;
        for (int i = 1; i < path.corners.Length; i++)
            total += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        return total;
    }

    // True if any segment of the path comes within radius of the point
    private static bool PathPassesNear(NavMeshPath path, Vector3 point, float radius)
    {
        for (int i = 1; i < path.corners.Length; i++)
        {
            Vector3 a = path.corners[i - 1];
            Vector3 ab = path.corners[i] - a;
            float lengthSq = ab.sqrMagnitude;
            float t = lengthSq > 0.0001f ? Mathf.Clamp01(Vector3.Dot(point - a, ab) / lengthSq) : 0f;

            if (Vector3.Distance(a + ab * t, point) < radius)
                return true;
        }
        return false;
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

    // ---- Sight, watching, and helpers ----

    // True when the monster can see the player: inside its field of view, and nothing solid in between.
    private bool CanSeePlayer()
    {
        if (player == null) return false;

        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 toPlayer = player.position + Vector3.up - eye;
        float distance = toPlayer.magnitude;

        if (distance > sightRange || distance < 0.01f) return false;
        if (Vector3.Angle(transform.forward, toPlayer) > sightAngle * 0.5f) return false;

        Transform hit = FirstSolidHit(eye, toPlayer / distance, distance, transform);
        return hit != null && hit.IsChildOf(player);
    }

    // True when the player is looking at the monster: inside their view cone, and nothing solid in between.
    private bool IsWatchedByPlayer()
    {
        if (viewCamera == null) return false;

        Vector3 eye = viewCamera.transform.position;
        Vector3 toMonster = transform.position + Vector3.up - eye;
        float distance = toMonster.magnitude;

        if (distance > watchRange || distance < 0.01f) return false;
        if (Vector3.Angle(viewCamera.transform.forward, toMonster) > watchAngle) return false;

        // The player's own body is ignored. If nothing else is in the way, the monster is visible.
        Transform hit = FirstSolidHit(eye, toMonster / distance, distance, player);
        return hit == null || hit.IsChildOf(transform);
    }

    // The first solid thing along a ray, ignoring the given transform. Null if nothing is hit.
    private static Transform FirstSolidHit(Vector3 origin, Vector3 direction, float distance, Transform ignore)
    {
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, distance, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (ignore != null && hit.transform.IsChildOf(ignore)) continue;
            return hit.transform;
        }

        return null;
    }

    // Snaps the target onto the NavMesh before setting it. Returns false if there's no NavMesh near it.
    private bool SetDestinationSafe(Vector3 target)
    {
        if (!NavMesh.SamplePosition(target, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            return false;

        agent.SetDestination(hit.position);
        return true;
    }


    private Vector3 RandomPointNear(Vector3 center, float radius)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector2 offset = Random.insideUnitCircle * radius;
            Vector3 candidate = center + new Vector3(offset.x, 0f, offset.y);
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                return hit.position;
        }
        return center;
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
