using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{

    [Header("Cameras")]
    [SerializeField] Camera mainCamera;
    [SerializeField] Camera weaponCamera;


    [Header("Look Sens")]
    [SerializeField] float sensX = 0.2f;
    [SerializeField] float sensY = 0.2f;


    [Header("FOV")]
    [SerializeField] float baseFov = 90f;
    [SerializeField] float maxFov = 140f;

    [Tooltip("Velocity magnitude offset before FOV starts increasing.")]
    [SerializeField] float fovSpeedOffset = 3.44f;
    [Tooltip("How fast FOV moves toward target in FixedUpdate (0..1).")]
    [Range(0f, 1f)]
    [SerializeField] float fovLerp = 0.5f;

    [Header("Wallrun Tilt")]
    [SerializeField] float wallRunTilt = 15f;
    [Tooltip("How fast tilt interpolates in FixedUpdate (0..1).")]
    [Range(0f, 1f)]
    [SerializeField] float tiltLerp = 0.05f;

    [Header("Sway / Punch")]
    [Tooltip("How fast sway returns to zero in FixedUpdate (0..1).")]
    [Range(0f, 1f)]
    [SerializeField] float swayReturnLerp = 0.2f;

    float wishTilt = 0;
    float curTilt = 0;
    Vector2 currentLook;
    Vector2 sway = Vector3.zero;
    float fov;
    Rigidbody rb;
    PlayerControls controls;


    void Awake()
    {
        controls = new PlayerControls();
        rb = GetComponentInParent<Rigidbody>();
    }
    void OnEnable() { controls.Enable(); }
    void OnDisable() { controls.Disable(); }
    void Start()
    {
        curTilt = transform.localEulerAngles.z;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        fov = baseFov;
    }

    void FixedUpdate()
    {
        float addedFov = rb.linearVelocity.magnitude - fovSpeedOffset;
        float targetFov = Mathf.Clamp(baseFov + addedFov, baseFov, maxFov);
        fov = Mathf.Lerp(fov, targetFov, fovLerp); // Smooth damping
        fov = Mathf.Clamp(fov, baseFov, maxFov);

        curTilt = Mathf.LerpAngle(curTilt, wishTilt * wallRunTilt, tiltLerp);
        sway = Vector2.Lerp(sway, Vector2.zero, swayReturnLerp);
    }
    void LateUpdate()
    {
        RotateMainCamera(); // Run the rotation logic here!

        // Apply the calculated FOV here to prevent jitter
        mainCamera.fieldOfView = fov;
        weaponCamera.fieldOfView = fov;
    }

    void RotateMainCamera()
    {
        Vector2 mouseInput = controls.Player.Look.ReadValue<Vector2>();
        mouseInput.x *= sensX;
        mouseInput.y *= sensY;

        currentLook.x += mouseInput.x;
        currentLook.y = Mathf.Clamp(currentLook.y += mouseInput.y, -90, 90);

        transform.localRotation = Quaternion.Euler(-currentLook.y + sway.y, sway.x, curTilt);
        transform.root.transform.localRotation = Quaternion.Euler(0, currentLook.x, 0);
    }

    public void Punch(Vector2 dir)
    {
        sway += dir;
    }

    #region Setters
    public void SetTilt(float newVal)
    {
        wishTilt = newVal;
    }

    public void SetXSens(float newVal)
    {
        sensX = newVal;
    }

    public void SetYSens(float newVal)
    {
        sensY = newVal;
    }

    public void SetFov(float newVal)
    {
        baseFov = newVal;
    }
    #endregion
}