// MonsterAI
// Chases the player across the NavMesh. If the player is in a SafeRoom with its lights
// on, the monster waits at that room's Stare Point and watches. Once the lights are off,
// it comes in. Getting close to the player ends the run. A flash stuns it.
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class MonsterAI : MonoBehaviour
{
    [SerializeField] private float attackDistance = 1.5f;
    [SerializeField] private float turnSpeed = 6f;
    [SerializeField] private Animator animator;   // The Animator on the model (a child object)
    [Header("Speeds (match the walk and run clips)")]
    [SerializeField] private float walkSpeed = 1.5f;   // Used while waiting at a Stare Point
    [SerializeField] private float runSpeed = 3.5f;    // Used while chasing the player
    private PlayerLife playerLife;
    [SerializeField] private float attackInterval = 1.5f;   // Seconds between the monster's attacks


    private float attackCooldown;


    private NavMeshAgent agent;
    private Transform player;
    private float stunTimer;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        FirstPersonController fpc = FindObjectOfType<FirstPersonController>();
        if (fpc != null)
        {
            player = fpc.transform;
            playerLife = fpc.GetComponent<PlayerLife>();
        }
    }

    void Update()
    {
        if (player == null) return;

        Vector3 playerPos = player.position;
        AudioManager.Instance?.UpdateMonsterGrowl(transform.position, playerPos);

        if (stunTimer > 0f)
        {
            stunTimer -= Time.deltaTime;
            agent.isStopped = true;
            return;
        }
        attackCooldown -= Time.deltaTime;

        // Speed is 0 when standing still and 1 at full run speed. Smoothing it keeps the blend natural.
        float speed01 = agent.velocity.magnitude / Mathf.Max(agent.speed, 0.01f);
        animator.SetFloat("Speed", agent.velocity.magnitude, 0.15f, Time.deltaTime);


        SafeRoom room = SafeRoom.Containing(playerPos);
        bool blocked = room != null && room.LightsOn && room.StarePoint != null;
        if (Time.frameCount % 60 == 0)
        {
            Debug.Log($"Monster: status={agent.pathStatus} hasPath={agent.hasPath} pending={agent.pathPending} dest={agent.destination} player={playerPos} monster={transform.position}");
        }

        if (blocked)
        {
            // Wait outside the room and watch the player
            agent.speed = walkSpeed;
            agent.isStopped = false;
            agent.SetDestination(room.StarePoint.position);

            if (Vector3.Distance(transform.position, room.StarePoint.position) < 1f)
            {
                agent.isStopped = true;
                FaceTarget(playerPos);
            }
        }
                else
        {
            agent.speed = runSpeed;
            agent.isStopped = false;
            agent.SetDestination(playerPos);

            if (Vector3.Distance(transform.position, playerPos) <= attackDistance && attackCooldown <= 0f)
            {
                animator.SetTrigger("Attack");
                attackCooldown = attackInterval;
                playerLife?.TakeHit();
            }
        }

    }

    // Called by FlashDevice when the flash hits this monster.
    public void Stun(float seconds)
    {
        stunTimer = Mathf.Max(stunTimer, seconds);
        animator.SetTrigger("Hit");
    }


    void FaceTarget(Vector3 target)
    {
        Vector3 direction = target - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), turnSpeed * Time.deltaTime);
    }
}
