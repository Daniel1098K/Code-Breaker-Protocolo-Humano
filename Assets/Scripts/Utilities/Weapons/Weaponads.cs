using System.Collections;
using UnityEngine;

/// <summary>
/// WeaponADS.cs - Code-Breaker: Protocolo Humano
/// Maneja el sistema de apuntado (ADS) para la Pistola Ping y el Rifle Kernel.
/// Adjuntar al mismo GameObject que PlayerShooting (el Player).
/// Asignar en el Inspector: pingModel, kernelModel y playerCamera.
/// </summary>
public class WeaponADS : MonoBehaviour
{
    public static WeaponADS Instance { get; private set; }

    // ─── Referencias ───────────────────────────────────────────────────────────
    [Header("References")]
    public Camera playerCamera;
    public GameObject pingModel;
    public GameObject kernelModel;

    // ─── Velocidad de transición ───────────────────────────────────────────────
    [Header("ADS Settings")]
    [Tooltip("Velocidad de interpolación entre hip y ADS (mayor = más rápido)")]
    public float adsSpeed = 12f;

    // ─── Ping — Posiciones ─────────────────────────────────────────────────────
    [Header("── Ping Pistol ──")]
    public Vector3 pingHipPosition = new Vector3(-0.03f,  -0.33f,  0.69f);
    public Vector3 pingHipRotation = new Vector3( 0f,      99.01f,  0f);
    public Vector3 pingADSPosition = new Vector3(-0.371f, -0.23f,   0.345f);
    public Vector3 pingADSRotation = new Vector3( 0f,      91.909f, 0f);

    [Tooltip("FOV de cámara en ADS para la Ping (0 = sin cambio de FOV)")]
    public float pingADSFov = 0f;

    // ─── Kernel — Posiciones ───────────────────────────────────────────────────
    [Header("── Kernel Rifle ──")]
    public Vector3 kernelHipPosition = new Vector3( 0.24f,  -0.38f,  0.67f);
    public Vector3 kernelHipRotation = new Vector3(-0.136f,  99.043f, 0.516f);
    public Vector3 kernelADSPosition = new Vector3(-0.174f, -0.315f,  0.886f);
    public Vector3 kernelADSRotation = new Vector3(-0.052f,  89.943f, 0.531f);

    [Tooltip("FOV de cámara en ADS para el Kernel (zoom x2 = mitad del FOV base)")]
    public float kernelADSFov = 30f; // FOV base 60 / 2 = 30 → zoom x2

    // ─── Estado interno ────────────────────────────────────────────────────────
    private bool isADS = false;
    private float baseFov;

    private GameObject activeModel;
    private Vector3 currentHipPos;
    private Vector3 currentHipRot;
    private Vector3 currentADSPos;
    private Vector3 currentADSRot;
    private float   currentADSFov;

    public bool IsADS => isADS;

    // ──────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        baseFov = (playerCamera != null && playerCamera.fieldOfView > 1f)
              ? playerCamera.fieldOfView
              : 60f;

        SetWeapon(PlayerShooting.WeaponType.PingPistol);
    }

    void Update()
    {
        HandleADSInput();
        ApplyADS();
    }

    // ─── Input ─────────────────────────────────────────────────────────────────
    void HandleADSInput()
    {
        PlayerShooting ps = PlayerShooting.Instance;
        if (ps == null) return;

        // Solo Ping y Kernel tienen ADS
        bool weaponSupportsADS = ps.CurrentWeapon == PlayerShooting.WeaponType.PingPistol
                              || ps.CurrentWeapon == PlayerShooting.WeaponType.KernelRifle;

        // RMB mantenido = ADS; soltar = hip
        isADS = weaponSupportsADS && Input.GetMouseButton(1);
    }

    // ─── Interpolación suave ───────────────────────────────────────────────────
    void ApplyADS()
    {
        if (activeModel == null) return;

        Vector3 targetPos = isADS ? currentADSPos : currentHipPos;
        Vector3 targetRot = isADS ? currentADSRot : currentHipRot;
        float   targetFov = isADS && currentADSFov > 0f ? currentADSFov : baseFov;

        // Posición y rotación del modelo
        activeModel.transform.localPosition = Vector3.Lerp(
            activeModel.transform.localPosition,
            targetPos,
            Time.deltaTime * adsSpeed);

        activeModel.transform.localRotation = Quaternion.Slerp(
            activeModel.transform.localRotation,
            Quaternion.Euler(targetRot),
            Time.deltaTime * adsSpeed);

        // FOV de cámara (zoom)
        if (playerCamera != null)
        {
            playerCamera.fieldOfView = Mathf.Lerp(
                playerCamera.fieldOfView,
                targetFov,
                Time.deltaTime * adsSpeed);
        }
    }

    // ─── API Pública ───────────────────────────────────────────────────────────

    /// <summary>
    /// Llamar desde PlayerShooting.SwitchWeapon() al cambiar de arma.
    /// Actualiza qué modelo y posiciones usar.
    /// </summary>
    public void SetWeapon(PlayerShooting.WeaponType weapon)
    {
        isADS = false;

        // Restaurar FOV inmediatamente al cambiar de arma
        if (playerCamera != null)
            playerCamera.fieldOfView = baseFov;

        switch (weapon)
        {
            case PlayerShooting.WeaponType.PingPistol:
                activeModel    = pingModel;
                currentHipPos  = pingHipPosition;
                currentHipRot  = pingHipRotation;
                currentADSPos  = pingADSPosition;
                currentADSRot  = pingADSRotation;
                currentADSFov  = pingADSFov;
                break;

            case PlayerShooting.WeaponType.KernelRifle:
                activeModel    = kernelModel;
                currentHipPos  = kernelHipPosition;
                currentHipRot  = kernelHipRotation;
                currentADSPos  = kernelADSPosition;
                currentADSRot  = kernelADSRotation;
                currentADSFov  = kernelADSFov;
                break;

            case PlayerShooting.WeaponType.ThermalBlade:
                activeModel   = null; // Blade no tiene ADS
                currentADSFov = 0f;
                break;
        }
    }

    /// <summary>
    /// Fuerza salida de ADS instantánea sin lerp.
    /// Llamar desde TerminalController al abrir hackeo.
    /// </summary>
    public void ForceHip()
    {
        isADS = false;

        if (playerCamera != null)
            playerCamera.fieldOfView = baseFov;

        if (activeModel != null)
        {
            activeModel.transform.localPosition = currentHipPos;
            activeModel.transform.localRotation = Quaternion.Euler(currentHipRot);
        }
    }
}