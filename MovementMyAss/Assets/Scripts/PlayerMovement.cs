using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Ground Movement")]
    [SerializeField] float groundSpeed = 10f;
    [SerializeField] float runSpeed = 16f;
    [SerializeField] float grAccel = 14f;

    [Header("Ground Braking")]
    [Tooltip("How quickly horizontal velocity decays when no input (units: m/s per second).")]
    [SerializeField] float groundFriction = 15f;
    [Tooltip("Below this horizontal speed we hard-stop to prevent micro sliding.")]
    [SerializeField] float stopSpeed = 0.08f;

    [Header("Air Movement")]
    [SerializeField] float airSpeed = 6f;
    [SerializeField] float airAccel = 20f;

    [Header("Jump")]
    [SerializeField] float jumpUpSpeed = 16f;
    [SerializeField] float dashSpeed = 6f;

    [Header("Wall Movement")]
    [SerializeField] float wallSpeed = 18f;
    [SerializeField] float wallClimbSpeed = 4f;
    [SerializeField] float wallAccel = 50f;
    [SerializeField] float wallRunTime = 3f;
    [SerializeField] float wallStickiness = 20f;
    [SerializeField] float wallStickDistance = 1f;
    [SerializeField] float wallFloorBarrier = 40f;
    [SerializeField] float wallBanTime = 4f;

    [Header("Ground Detection")]
    [SerializeField] LayerMask groundMask;
    [SerializeField] float groundCheckDistance = 0.8f;

    // [Header("Slide")]
    // [SerializeField] float slideBoost = 4f;          // instant speed add on slide start
    // [SerializeField] float slideMinStartSpeed = 8f;  // must be moving at least this fast to start
    // [SerializeField] float slideDuration = 0.9f;     // max time sliding
    // [SerializeField] float slideFriction = 6f;       // how quickly slide slows down (m/s per second)
    // [SerializeField] float slideMinSpeed = 3f;       // exit slide below this speed
    // [SerializeField] float slideSteerAccel = 10f;    // how much you can steer during slide
    // [SerializeField] float slideCooldown = 0.25f;    // prevents slide spam
    // [SerializeField] float slideDamping = 0f;        // keep low to preserve momentum
    // [SerializeField] bool slideCancelOnRelease = true;


    [Header("Input")]
    [SerializeField] float inputDeadzone = 0.1f;

    [Header("Damping")]
    [SerializeField] float groundDamping = 2f;
    [SerializeField] float airDamping = 0f;

    [Header("Debug")]
    [SerializeField] Mode mode = Mode.Flying;

    // Cooldowns / timers
    bool canJump = true;
    bool canDJump = true;
    float wallBan = 0f;
    float wrTimer = 0f;
    float wallStickTimer = 0f;

    float slideTimer = 0f;
    float slideCD = 0f;

    bool crouchPressed; // one-frame input
    bool crouchReleased;

    Vector3 slideDir = Vector3.forward;


    // Prevent re-grounding immediately after jump
    float ignoreGroundTimer = 0f;
    [SerializeField] float ignoreGroundAfterJump = 0.10f;

    public event Action OnJump;
    public event Action OnDoubleJump;
    public event Action OnStartWallRun;
    public event Action OnEndWallRun;
    public event Action OnStartWalking;

    PlayerControls controls;

    // State
    bool running;
    bool jumpRequested;
    bool crouched;
    bool grounded;

    Collider ground;
    Vector3 groundNormal = Vector3.up;
    Vector3 bannedGroundNormal = Vector3.zero;

    CapsuleCollider col;
    CameraController camCon;
    Rigidbody rb;

    Vector3 wishDir = Vector3.zero;

    enum Mode
    {
        Walking,
        Flying,
        Wallruning,
        // Sliding
    }

    void Awake()
    {
        controls = new PlayerControls();
    }

    void OnEnable() => controls.Enable();
    void OnDisable() => controls.Disable();

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        camCon = GetComponentInChildren<CameraController>();
        col = GetComponent<CapsuleCollider>();
    }

    void Update()
    {
        wishDir = ReadWishDirection();

        Vector2 moveInput = controls.Player.Move.ReadValue<Vector2>();
        bool sprintHeld = controls.Player.Sprint.IsPressed();

        running = sprintHeld && moveInput.y > 0.5f;
        crouched = controls.Player.Crouch.IsPressed();
        crouchPressed = controls.Player.Crouch.WasPressedThisFrame();
        crouchReleased = controls.Player.Crouch.WasReleasedThisFrame();


        if (controls.Player.Jump.WasPressedThisFrame())
            jumpRequested = true;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        // Crouch collider (use fixed dt here, since we're in FixedUpdate)
        if (crouched)
            col.height = Mathf.Max(0.6f, col.height - dt * 10f);
        else
            col.height = Mathf.Min(1.8f, col.height + dt * 10f);

        // Update timers
        wallStickTimer = Mathf.Max(wallStickTimer - dt, 0f);
        wallBan = Mathf.Max(wallBan - dt, 0f);
        ignoreGroundTimer = Mathf.Max(ignoreGroundTimer - dt, 0f);
        slideTimer = Mathf.Max(slideTimer - dt, 0f);
        slideCD = Mathf.Max(slideCD - dt, 0f);


        // Ban logic (unchanged intent, but uses fixed dt + robust)
        bannedGroundNormal = (wallStickTimer == 0f && wallBan > 0f) ? groundNormal : Vector3.zero;

        // Update grounded + mode (DO NOT skip because jump was pressed)
        UpdateGroundAndMode();

        // Apply damping based on actual state (Walking+grounded => ground damping, else air)
        ApplyDamping();

        // Movement
        switch (mode)
        {
            case Mode.Wallruning:
                camCon?.SetTilt(WallrunCameraAngle());
                Wallrun(wishDir, wallSpeed, wallClimbSpeed, wallAccel);

                if (ground != null && !ground.CompareTag("InfiniteWallrun"))
                    wrTimer = Mathf.Max(wrTimer - dt, 0f);
                break;

            case Mode.Walking:
                camCon?.SetTilt(0f);
                // Start slide when sprinting + grounded + press crouch + not on cooldown
                if (crouchPressed && running && slideCD <= 0f)
                {
                    Vector3 v = rb.linearVelocity;
                    Vector3 horiz = new Vector3(v.x, 0f, v.z);

                    // if (horiz.magnitude >= slideMinStartSpeed)
                    // {
                    //     EnterSliding(horiz);
                    //     break;
                    // }
                }

                Walk(wishDir, running ? runSpeed : groundSpeed, grAccel);
                break;

            case Mode.Flying:
                camCon?.SetTilt(0f);
                AirMove(wishDir, airSpeed, airAccel);
                break;

                // case Mode.Sliding:
                //     camCon?.SetTilt(0f);
                //     SlideMove(wishDir);
                //     break;

        }

        // Consume one-frame jump input
        jumpRequested = false;
    }

    // -------- Input / Direction --------
    Vector3 ReadWishDirection()
    {
        Vector2 input = controls.Player.Move.ReadValue<Vector2>();
        if (input.magnitude < inputDeadzone)
            input = Vector2.zero;

        Vector3 local = new Vector3(input.x, 0f, input.y);
        return transform.TransformDirection(local);
    }

    // -------- Grounding & State Machine --------
    void UpdateGroundAndMode()
    {
        // If sliding and we leave ground, go to flying
        // if (mode == Mode.Sliding)
        // {
        //     grounded = CheckGrounded();
        //     if (!grounded)
        //         EnterFlying(true);
        //     return;
        // }

        // While wallrunning: if we're clearly on the ground, immediately return to Walking
        if (mode == Mode.Wallruning)
        {
            if (ignoreGroundTimer <= 0f && CheckGrounded())
            {
                grounded = true;
                EnterWalking();
            }
            return;
        }


        // After jumping, ignore ground briefly so we don't instantly re-enter Walking
        if (ignoreGroundTimer > 0f)
        {
            grounded = false;
            if (mode == Mode.Walking)
                EnterFlying(true);
            return;
        }

        grounded = CheckGrounded();

        if (grounded)
        {
            EnterWalking();
        }
        else
        {
            EnterFlying(false);
        }
    }

    void EnterWalking(float impactSpeed = 0f)
    {
        if (mode == Mode.Walking)
            return;

        if (mode == Mode.Wallruning)
            OnEndWallRun?.Invoke();

        OnStartWalking?.Invoke();
        mode = Mode.Walking;
    }


    void EnterFlying(bool wishFly)
    {
        if (mode == Mode.Flying)
            return;

        // If leaving wallrun without explicit wishFly, you had some "stickiness" logic.
        if (mode == Mode.Wallruning && VectorToWall().magnitude < wallStickDistance && !wishFly)
            return;

        if (mode == Mode.Wallruning)
            OnEndWallRun?.Invoke();

        wallBan = wallBanTime;
        canDJump = true;

        mode = Mode.Flying;
    }

    void EnterWallrun()
    {
        if (mode == Mode.Wallruning)
            return;

        // require not near ground + not banned + not sticky timer
        if (VectorToGround().magnitude > 0.2f && CanRunOnThisWall(bannedGroundNormal) && wallStickTimer == 0f)
        {
            OnStartWallRun?.Invoke();
            wrTimer = wallRunTime;
            canDJump = true;
            mode = Mode.Wallruning;
        }
        else
        {
            EnterFlying(true);
        }
    }

    // void EnterSliding(Vector3 currentHoriz)
    // {
    //     // Enter slide
    //     mode = Mode.Sliding;
    //     slideTimer = slideDuration;
    //     slideCD = slideCooldown;

    //     // Slide direction: mostly preserve momentum direction
    //     if (currentHoriz.sqrMagnitude > 0.001f)
    //         slideDir = currentHoriz.normalized;
    //     else
    //     {
    //         slideDir = transform.forward;
    //         slideDir.y = 0f;
    //         slideDir.Normalize();
    //     }

    //     // Apply boost along slide direction
    //     float speed = currentHoriz.magnitude;
    //     float newSpeed = speed + slideBoost;

    //     Vector3 v = rb.linearVelocity;
    //     Vector3 newHoriz = slideDir * newSpeed;
    //     rb.linearVelocity = new Vector3(newHoriz.x, v.y, newHoriz.z);
    // }


    // -------- Movement --------
    void Walk(Vector3 wish, float maxSpeed, float acceleration)
    {
        if (jumpRequested && canJump)
        {
            OnJump?.Invoke();
            Jump();
            return;
        }

        Vector3 v = rb.linearVelocity;
        Vector3 horiz = new Vector3(v.x, 0f, v.z);

        float wishMag = wish.magnitude;

        // No input => apply deterministic friction braking
        if (wishMag < inputDeadzone)
        {
            float dt = Time.fixedDeltaTime;
            horiz = Vector3.MoveTowards(horiz, Vector3.zero, groundFriction * dt);

            if (horiz.magnitude < stopSpeed)
                horiz = Vector3.zero;

            rb.linearVelocity = new Vector3(horiz.x, v.y, horiz.z);
            return;
        }

        Vector3 wishDir = wish / wishMag;

        // Accelerate toward target horizontal velocity
        Vector3 target = wishDir * maxSpeed;
        Vector3 force = (target - horiz) * acceleration;

        // Slope correction: guard against near-zero y normals
        if (groundNormal.y > 0.01f)
        {
            Vector3 slopeCorrection = groundNormal * Physics.gravity.y / groundNormal.y;
            slopeCorrection.y = 0f;
            force += slopeCorrection;
        }

        rb.AddForce(force, ForceMode.Acceleration);
    }

    void AirMove(Vector3 wish, float maxSpeed, float acceleration)
    {
        // Double jump
        if (jumpRequested && !crouched)
        {
            OnDoubleJump?.Invoke();
            DoubleJump(wish);
        }

        if (crouched && rb.linearVelocity.y > -10f && controls.Player.Jump.IsPressed())
            rb.AddForce(Vector3.down * 20f, ForceMode.Acceleration);

        // Horizontal velocity (before steering)
        Vector3 v0 = rb.linearVelocity;
        Vector3 horiz0 = new Vector3(v0.x, 0f, v0.z);
        float keepSpeed = horiz0.magnitude;

        // Wish in local space so we can tell forward vs strafe components
        Vector3 localWish = transform.InverseTransformDirection(wish);
        localWish.y = 0f;

        float mag = localWish.magnitude;
        if (mag < 0.0001f)
            return;

        Vector3 localDir = localWish / mag; // normalized direction in local space

        bool hasForward = Mathf.Abs(localDir.z) > 0.001f;
        bool hasStrafe = Mathf.Abs(localDir.x) > 0.001f;
        bool diagonal = hasForward && hasStrafe;

        // Diagonal: allow only half the strafe influence compared to pure strafe
        float strafeCap = maxSpeed * (diagonal ? 0.5f : 1f);
        float forwardCap = maxSpeed;

        // Apply acceleration separately along right and forward
        AirAccelAxis(transform.right, localDir.x, strafeCap, acceleration);
        AirAccelAxis(transform.forward, localDir.z, forwardCap, acceleration);

        // Re-normalize horizontal speed so steering trades forward for sideways
        // (prevents "free speed gain" from adding components)
        Vector3 v1 = rb.linearVelocity;
        Vector3 horiz1 = new Vector3(v1.x, 0f, v1.z);

        if (keepSpeed > 0.0001f && horiz1.magnitude > keepSpeed)
        {
            horiz1 = horiz1.normalized * keepSpeed;
            rb.linearVelocity = new Vector3(horiz1.x, v1.y, horiz1.z);
        }
    }


    void Wallrun(Vector3 wishDir, float maxSpeed, float climbSpeed, float acceleration)
    {
        if (jumpRequested)
        {
            // Vertical
            float upForce = Mathf.Clamp(jumpUpSpeed - rb.linearVelocity.y, 0f, Mathf.Infinity);
            rb.AddForce(new Vector3(0f, upForce, 0f), ForceMode.VelocityChange);

            // Horizontal off wall
            Vector3 jumpOffWall = groundNormal.normalized * dashSpeed;
            jumpOffWall.y = 0f;
            rb.AddForce(jumpOffWall, ForceMode.VelocityChange);

            wrTimer = 0f;
            EnterFlying(true);
            return;
        }

        if (wrTimer <= 0f || crouched)
        {
            rb.AddForce(groundNormal * 3f, ForceMode.VelocityChange);
            EnterFlying(true);
            return;
        }

        // Horizontal along wall plane
        Vector3 distance = VectorToWall();
        if (distance == Vector3.positiveInfinity)
        {
            wallStickTimer = 0.2f;
            EnterFlying(true);
            return;
        }

        Vector3 wishOnPlane = RotateToPlane(wishDir, -distance.normalized);
        wishOnPlane *= maxSpeed;
        wishOnPlane.y = Mathf.Clamp(wishOnPlane.y, -climbSpeed, climbSpeed);

        Vector3 wallrunForce = wishOnPlane - rb.linearVelocity;
        if (wallrunForce.magnitude > 0.2f)
            wallrunForce = wallrunForce.normalized * acceleration;

        if (rb.linearVelocity.y < 0f && wishOnPlane.y > 0f)
            wallrunForce.y = 2f * acceleration;

        // Anti-gravity
        Vector3 antiGravityForce = -Physics.gravity;
        if (wrTimer < 0.33f * wallRunTime)
        {
            antiGravityForce *= wrTimer / wallRunTime;
            wallrunForce += (Physics.gravity + antiGravityForce);
        }

        rb.AddForce(wallrunForce, ForceMode.Acceleration);
        rb.AddForce(antiGravityForce, ForceMode.Acceleration);

        if (distance.magnitude > wallStickDistance)
            distance = Vector3.zero;

        rb.AddForce(distance * wallStickiness, ForceMode.Acceleration);
    }

    // void SlideMove(Vector3 wish)
    // {
    //     // End slide conditions
    //     Vector3 v = rb.linearVelocity;
    //     Vector3 horiz = new Vector3(v.x, 0f, v.z);

    //     if (!grounded)
    //     {
    //         EnterFlying(true);
    //         return;
    //     }

    //     if (slideTimer <= 0f || horiz.magnitude <= slideMinSpeed)
    //     {
    //         EnterWalking();
    //         return;
    //     }

    //     if (slideCancelOnRelease && crouchReleased)
    //     {
    //         EnterWalking();
    //         return;
    //     }

    //     // Keep slide direction aligned with motion (momentum-preserving)
    //     if (horiz.sqrMagnitude > 0.001f)
    //         slideDir = horiz.normalized;

    //     // Apply slide friction (gentler than ground braking)
    //     float dt = Time.fixedDeltaTime;
    //     horiz = Vector3.MoveTowards(horiz, Vector3.zero, slideFriction * dt);

    //     // Optional: small steering (won't overpower momentum)
    //     float wishMag = wish.magnitude;
    //     if (wishMag > inputDeadzone)
    //     {
    //         Vector3 wishDir = (wish / wishMag);
    //         Vector3 steer = Vector3.ProjectOnPlane(wishDir, Vector3.up) * slideSteerAccel;
    //         rb.AddForce(steer, ForceMode.Acceleration);
    //     }

    //     rb.linearVelocity = new Vector3(horiz.x, v.y, horiz.z);
    // }



    void Jump()
    {
        if (mode != Mode.Walking || !canJump)
            return;

        float upForce = Mathf.Clamp(jumpUpSpeed - rb.linearVelocity.y, 0f, Mathf.Infinity);
        rb.AddForce(new Vector3(0f, upForce, 0f), ForceMode.VelocityChange);

        ignoreGroundTimer = ignoreGroundAfterJump;
        StartCoroutine(JumpCooldownCoroutine(0.2f));
        EnterFlying(true);
    }

    void DoubleJump(Vector3 wishDir)
    {
        if (!canDJump)
            return;

        float upForce = Mathf.Clamp(jumpUpSpeed - rb.linearVelocity.y, 0f, Mathf.Infinity);
        rb.AddForce(new Vector3(0f, upForce, 0f), ForceMode.VelocityChange);

        // Horizontal dash influence (kept close to your logic)
        if (wishDir != Vector3.zero)
        {
            Vector3 horSpd = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            Vector3 newSpd = wishDir.normalized;
            float newSpdMagnitude = dashSpeed;

            if (horSpd.magnitude > dashSpeed)
            {
                float dot = Vector3.Dot(wishDir.normalized, horSpd.normalized);
                if (dot > 0f)
                    newSpdMagnitude = dashSpeed + (horSpd.magnitude - dashSpeed) * dot;
                else
                    newSpdMagnitude = Mathf.Clamp(dashSpeed * (1f + dot),
                        dashSpeed * (dashSpeed / Mathf.Max(horSpd.magnitude, 0.0001f)),
                        dashSpeed);
            }

            newSpd *= newSpdMagnitude;
            rb.AddForce(newSpd - horSpd, ForceMode.VelocityChange);
        }

        ignoreGroundTimer = ignoreGroundAfterJump;
        canDJump = false;
    }

    // -------- Collisions (used mainly for wallrun entry + better normals) --------
    void OnCollisionStay(Collision collision)
    {
        if (collision.contactCount <= 0)
            return;

        // Floor / wall classification from contact normals
        foreach (ContactPoint contact in collision.contacts)
        {
            float angle = Vector3.Angle(contact.normal, Vector3.up);

            // Floor-like
            if (angle < wallFloorBarrier)
            {
                groundNormal = contact.normal;
                ground = contact.otherCollider;

                if (ignoreGroundTimer <= 0f)
                {
                    grounded = true;
                    EnterWalking(collision.relativeVelocity.magnitude);
                }
                return;
            }
        }

        // Wallrun entry (only when not walking and not on banned walls)
        if (mode != Mode.Walking)
        {
            foreach (ContactPoint contact in collision.contacts)
            {
                if (contact.otherCollider.CompareTag("NoWallrun") || contact.otherCollider.CompareTag("Player"))
                    continue;

                float angle = Vector3.Angle(contact.normal, Vector3.up);
                if (angle > wallFloorBarrier && angle < 120f)
                {
                    grounded = true;               // "touching wall" for wallrun logic
                    groundNormal = contact.normal;
                    ground = contact.otherCollider;
                    EnterWallrun();
                    return;
                }
            }
        }
    }

    void OnCollisionExit(Collision collision)
    {
        // If we exited the wall collider we were wallrunning on, stop wallrun immediately.
        if (mode == Mode.Wallruning && ground != null && collision.collider == ground)
        {
            grounded = false;
            wallStickTimer = 0.2f;   // same value you use elsewhere
            EnterFlying(true);
        }
    }


    // -------- Helpers --------
    void ApplyDamping()
    {
        // Ground damping only when actually walking on ground.
        float damping =
    (mode == Mode.Walking && grounded) ? groundDamping :
    // (mode == Mode.Sliding && grounded) ? slideDamping :
    airDamping;


#if UNITY_6000_0_OR_NEWER
        rb.linearDamping = damping;
#else
        rb.drag = damping;
#endif
    }

    bool CheckGrounded()
    {
        Vector3 origin = transform.position + Vector3.up * col.radius;
        float castDistance = (col.height / 2f) - col.radius + groundCheckDistance;

        if (Physics.SphereCast(origin, col.radius * 0.9f, Vector3.down,
                out RaycastHit hit, castDistance, groundMask))
        {
            if (Vector3.Angle(hit.normal, Vector3.up) < wallFloorBarrier)
            {
                groundNormal = hit.normal;
                ground = hit.collider;
                return true;
            }
        }

        return false;
    }

    // Math helpers (kept from your file)
    Vector3 RotateToPlane(Vector3 vect, Vector3 normal)
    {
        Vector3 rotDir = Vector3.ProjectOnPlane(normal, Vector3.up);
        Quaternion rotation = Quaternion.AngleAxis(-90f, Vector3.up);
        rotDir = rotation * rotDir;
        float angle = -Vector3.Angle(Vector3.up, normal);
        rotation = Quaternion.AngleAxis(angle, rotDir);
        return rotation * vect;
    }

    float WallrunCameraAngle()
    {
        Vector3 rotDir = Vector3.ProjectOnPlane(groundNormal, Vector3.up);
        Quaternion rotation = Quaternion.AngleAxis(-90f, Vector3.up);
        rotDir = rotation * rotDir;

        float angle = Vector3.SignedAngle(Vector3.up, groundNormal, Quaternion.AngleAxis(90f, rotDir) * groundNormal);
        angle -= 90f;
        angle /= 180f;

        Vector3 playerDir = transform.forward;
        Vector3 normal = new Vector3(groundNormal.x, 0f, groundNormal.z);

        return Vector3.Cross(playerDir, normal).y * angle;
    }

    bool CanRunOnThisWall(Vector3 normal)
    {
        return (Vector3.Angle(normal, groundNormal) > 10f) || (wallBan == 0f);
    }

    Vector3 VectorToWall()
    {
        Vector3 position = transform.position + Vector3.up * col.height / 2f;

        if (Physics.Raycast(position, -groundNormal, out RaycastHit hit, wallStickDistance) &&
            Vector3.Angle(groundNormal, hit.normal) < 70f &&
            ground != null && hit.collider == ground)   // NEW: must be same wall collider
        {
            groundNormal = hit.normal;
            return hit.point - position;
        }

        return Vector3.positiveInfinity;
    }


    Vector3 VectorToGround()
    {
        Vector3 position = transform.position;

        if (Physics.Raycast(position, Vector3.down, out RaycastHit hit, wallStickDistance))
            return hit.point - position;

        return Vector3.positiveInfinity;
    }

    IEnumerator JumpCooldownCoroutine(float time)
    {
        canJump = false;
        yield return new WaitForSeconds(time);
        canJump = true;
    }

    void AirAccelAxis(Vector3 axisWorld, float inputAmount, float maxAxisSpeed, float acceleration)
    {
        if (Mathf.Abs(inputAmount) < 0.001f)
            return;

        // Flatten axis to horizontal
        Vector3 axis = axisWorld;
        axis.y = 0f;
        if (axis.sqrMagnitude < 0.0001f)
            return;

        axis.Normalize();

        // Direction (+ or -) based on input sign
        axis *= Mathf.Sign(inputAmount);

        Vector3 horiz = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        // Current speed along this axis direction
        float cur = Vector3.Dot(horiz, axis);

        float dt = Time.fixedDeltaTime;
        float accelVel = acceleration * dt;

        // Clamp so we don't exceed the axis cap
        if (cur + accelVel > maxAxisSpeed)
            accelVel = Mathf.Max(0f, maxAxisSpeed - cur);

        rb.AddForce(axis * accelVel, ForceMode.VelocityChange);
    }

}