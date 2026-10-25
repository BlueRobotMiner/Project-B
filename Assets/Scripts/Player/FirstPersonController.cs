using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [SerializeField] private Transform cameraHolder;
    [SerializeField] private float walkSpeed = 2.5f;
    [SerializeField] private float sprintSpeed = 4.5f;
    [SerializeField] private float crouchSpeed = 1.2f;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float standHeight = 1.8f;
    [SerializeField] private float crouchHeight = 1f;
    [SerializeField] private float maxStamina = 5f;
    [SerializeField] private float staminaDrain = 1f;
    [SerializeField] private float staminaRegen = 0.6f;
    [SerializeField] private Animator animator;               // Drag the Animator that has Man_03 Controller
    [SerializeField] private float maxLookUp = 50f;     // Degrees the head can tilt back
    [SerializeField] private float maxLookDown = 60f;   // Degrees the head can tilt forward
    [SerializeField] private float animationDamping = 0.1f;   // Smooths blends between clips
    private Transform head;
    [SerializeField] private float eyeForward = 0.1f;   // How far in front of the head the camera sits
    private void Start()
    {
        if (animator != null)
            head = animator.GetBoneTransform(HumanBodyBones.Head);

        if (head == null)
            Debug.LogWarning("FirstPersonController: no head bone found. Check the Avatar is Humanoid.", this);
    }




    private CharacterController cc;
    private float pitch;
    private float verticalVelocity;
    private float stamina;

    // The wake-up sequence can turn this off until the screen fades in
    public bool MovementEnabled { get; set; } = true;
    public float Stamina => stamina;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        stamina = maxStamina;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (animator == null)
            Debug.LogWarning("FirstPersonController: no Animator assigned", this);
    }


    private void Update()
    {
        if (!MovementEnabled)
            return;

        Look();
        Move();
    }
    [SerializeField] private Vector3 headRotationOffset = Vector3.zero;   // Small fine-tuning only

    private void LateUpdate()
    {
        if (head == null)
            return;

        cameraHolder.position = head.position + transform.forward * eyeForward;

        // Face the camera's direction, keeping the camera's up vector so the head doesn't roll
        Quaternion lookRot = Quaternion.LookRotation(cameraHolder.forward, cameraHolder.up);
        head.rotation = lookRot * Quaternion.Euler(headRotationOffset);
    }
    private void Look()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(0f, mouseX, 0f);
        pitch = Mathf.Clamp(pitch - mouseY, -maxLookUp, maxLookDown);
        cameraHolder.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void Move()
    {
        bool crouching = Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.LeftControl);
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        bool moving = h != 0f || v != 0f;
        bool sprinting = Input.GetKey(KeyCode.LeftShift) && !crouching && stamina > 0f && moving;
        // Convert input into blend tree values
        // Speed: 0.5 = walk, 1 = run, negative = backwards
        // Direction: -1 = strafe left, 1 = strafe right
        if (animator != null)
        {
            float forwardAmount = v != 0f ? v : (h != 0f ? 1f : 0f);  // strafing alone counts as walking
            float animSpeed = forwardAmount * (sprinting ? 1f : 0.5f);

            animator.SetFloat("Speed", animSpeed, animationDamping, Time.deltaTime);
            animator.SetFloat("Direction", h, animationDamping, Time.deltaTime);
            animator.SetBool("IsCrouching", crouching);
        }

        // Stamina drains while sprinting and refills otherwise
        if (sprinting)
            stamina = Mathf.Max(0f, stamina - staminaDrain * Time.deltaTime);
        else
            stamina = Mathf.Min(maxStamina, stamina + staminaRegen * Time.deltaTime);

        float speed = crouching ? crouchSpeed : (sprinting ? sprintSpeed : walkSpeed);

        Vector3 input = transform.right * h + transform.forward * v;
        if (input.magnitude > 1f)
            input.Normalize();

        // Gravity: keep the controller pressed to the ground
        if (cc.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;

        cc.Move((input * speed + Vector3.up * verticalVelocity) * Time.deltaTime);

        // Crouch by shrinking the controller and lowering the camera
        float targetHeight = crouching ? crouchHeight : standHeight;
        cc.height = Mathf.Lerp(cc.height, targetHeight, 10f * Time.deltaTime);
        cc.center = new Vector3(0f, cc.height * 0.5f, 0f);
    }
    
}
