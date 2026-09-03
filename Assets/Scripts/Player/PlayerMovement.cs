using System.Collections;
using UnityEngine;

/// <summary>
/// PlayerMovement.cs - Code-Breaker: Protocolo Humano
/// Maneja el movimiento del jugador: caminar, correr, saltar, agacharse y deslizarse.
/// Requiere: CharacterController adjunto al GameObject.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    // ─── Configuración de Movimiento ───────────────────────────────────────────
    [Header("Movement Settings")]
    [Tooltip("Velocidad caminando (m/s)")]
    public float walkSpeed = 5f;

    [Tooltip("Velocidad corriendo (m/s) — +50% del walk")]
    public float runSpeed = 7.5f;

    [Tooltip("Fuerza de salto")]
    public float jumpForce = 5f;

    [Tooltip("Multiplicador de gravedad")]
    public float gravity = -19.62f; // 2x gravedad real para mejor game feel

    // ─── Configuración de Slide ────────────────────────────────────────────────
    [Header("Slide Settings")]
    [Tooltip("Velocidad durante el deslizamiento")]
    public float slideSpeed = 10f;

    [Tooltip("Duración del slide en segundos")]
    public float slideDuration = 1.5f;

    [Tooltip("Cooldown del slide en segundos")]
    public float slideCooldown = 3f;

    [Tooltip("Altura del CharacterController al agacharse/slidear")]
    public float crouchHeight = 1f;

    // ─── Configuración de Stamina ──────────────────────────────────────────────
    [Header("Stamina")]
    [Tooltip("Stamina máxima")]
    public float maxStamina = 100f;

    [Tooltip("Consumo de stamina por segundo al correr")]
    public float staminaDrainRate = 10f;

    [Tooltip("Regeneración de stamina por segundo cuando no se usa")]
    public float staminaRegenRate = 20f;

    [Tooltip("Stamina mínima para poder iniciar un sprint")]
    public float minStaminaToRun = 10f;

    // ─── Configuración de Cámara ───────────────────────────────────────────────
    [Header("Camera")]
    [Tooltip("Transform de la cámara (hijo del player)")]
    public Transform playerCamera;

    [Tooltip("Sensibilidad del ratón")]
    public float mouseSensitivity = 2f;

    [Tooltip("Límite vertical de la cámara")]
    public float verticalLookLimit = 80f;

    // ─── Privados ──────────────────────────────────────────────────────────────
    private CharacterController controller;
    private Vector3 velocity;           // Velocidad vertical (gravedad + salto)
    private float currentStamina;
    private float standingHeight;
    private Vector3 standingCenter;

    // Estados
    private bool isGrounded;
    private bool isSliding;
    private bool isCrouching;
    private bool canSlide = true;
    private bool isSprinting;

    // Cámara
    private float verticalRotation = 0f;

    // ─── Propiedades Públicas (para otros sistemas) ────────────────────────────
    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;
    public bool IsGrounded => isGrounded;
    public bool IsSprinting => isSprinting;
    public bool IsSliding => isSliding;
    public bool IsCrouching => isCrouching;

    // ──────────────────────────────────────────────────────────────────────────

    void Start()
    {
        controller = GetComponent<CharacterController>();
        currentStamina = maxStamina;

        // Guardar altura y centro originales para restaurar al dejar de agacharse
        standingHeight = controller.height;
        standingCenter = controller.center;

        // Bloquear y ocultar cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        CheckGrounded();
        HandleCameraLook();
        HandleMovement();
        HandleJump();
        HandleCrouch();
        HandleSlideInput();
        HandleStamina();
        ApplyGravity();
    }

    // ─── Ground Check ──────────────────────────────────────────────────────────
    void CheckGrounded()
    {
        // CharacterController.isGrounded puede ser inconsistente; doble verificación
        isGrounded = controller.isGrounded;

        if (isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f; // Mantener pegado al suelo
        }
    }

    // ─── Cámara ────────────────────────────────────────────────────────────────
    void HandleCameraLook()
    {
        // Reducir sensibilidad en ADS
        float adsMultiplier = (WeaponADS.Instance != null && WeaponADS.Instance.IsADS) ? 0.7f : 1f;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * adsMultiplier;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * adsMultiplier;

        // Rotación horizontal del cuerpo
        transform.Rotate(Vector3.up * mouseX);

        // Rotación vertical de la cámara (clampada)
        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -verticalLookLimit, verticalLookLimit);

        if (playerCamera != null)
            playerCamera.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
    }

    // ─── Movimiento Horizontal ─────────────────────────────────────────────────
    void HandleMovement()
    {
        if (isSliding) return; // Durante slide el movimiento lo controla la corrutina

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 move = transform.right * x + transform.forward * z;

        // Determinar velocidad
        bool wantsToRun = Input.GetKey(KeyCode.LeftShift) && currentStamina > minStaminaToRun;
        isSprinting = wantsToRun && move.magnitude > 0.1f && !isCrouching;

        float currentSpeed = isSprinting ? runSpeed : walkSpeed;

        controller.Move(move * currentSpeed * Time.deltaTime);
    }

    // ─── Salto ─────────────────────────────────────────────────────────────────
    void HandleJump()
    {
        // Altura de salto ≈ 2 metros según GDD
        // v = sqrt(2 * |gravity| * height) → con gravity=-19.62 y h=2 → v≈8.85
        if (Input.GetButtonDown("Jump") && isGrounded && !isCrouching && !isSliding)
        {
            velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
        }
    }

    // ─── Agacharse ─────────────────────────────────────────────────────────────
    void HandleCrouch()
    {
        if (isSliding) return;

        if (Input.GetKeyDown(KeyCode.C))
        {
            if (!isCrouching)
                StartCrouch();
            else
                TryStandUp();
        }
    }

    void StartCrouch()
    {
        isCrouching = true;
        controller.height = crouchHeight;
        controller.center = new Vector3(0f, crouchHeight / 2f, 0f);
    }

    void TryStandUp()
    {
        // Origen: parte superior del collider agachado (cabeza actual del jugador)
        Vector3 crouchTop = transform.position + Vector3.up * (crouchHeight / 2f);

        // Verificar solo el espacio adicional necesario para expandirse, con margen
        float neededClearance = (standingHeight - crouchHeight) + 0.1f;

        if (Physics.Raycast(crouchTop, Vector3.up, neededClearance))
            return; // Hay techo bloqueando, no puede levantarse

        isCrouching = false;

        // ORDEN CRÍTICO: center primero → el collider se recentra sin cambiar tamaño
        //                height después → crece hacia arriba, no hacia abajo
        controller.center = standingCenter;
        controller.height = standingHeight;
    }

    // ─── Slide ─────────────────────────────────────────────────────────────────
    void HandleSlideInput()
    {
        // Activación: Correr + Ctrl (tal como especifica el GDD)
        if (Input.GetKeyDown(KeyCode.LeftControl) && isSprinting && canSlide && isGrounded)
        {
            StartCoroutine(PerformSlide());
        }
    }

    IEnumerator PerformSlide()
    {
        isSliding   = true;
        canSlide    = false;
        isCrouching = true;

        // Forzar salida de ADS al slidear
        if (WeaponADS.Instance != null) WeaponADS.Instance.ForceHip();

        // Reducir altura del collider
        // ORDEN: height primero al achicar (el collider encoge hacia arriba, base fija)
        controller.height = crouchHeight;
        controller.center = new Vector3(0f, crouchHeight / 2f, 0f);

        // Consumir stamina instantánea del slide
        currentStamina = Mathf.Max(0f, currentStamina - 30f);

        float elapsed        = 0f;
        Vector3 slideDir     = transform.forward;
        float startSpeed     = slideSpeed;

        while (elapsed < slideDuration)
        {
            float t            = elapsed / slideDuration;
            float currentSpeed = Mathf.Lerp(startSpeed, 0f, t); // deceleración suave

            // Combinar movimiento horizontal + gravedad en un solo Move()
            // para que CharacterController detecte suelos y paredes correctamente
            velocity.y += gravity * Time.deltaTime;
            if (controller.isGrounded && velocity.y < 0f)
                velocity.y = -2f;

            Vector3 move = (slideDir * currentSpeed) + new Vector3(0f, velocity.y, 0f);
            controller.Move(move * Time.deltaTime);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // ── Fin del slide — restauración segura del collider ──────────────────
        isSliding = false;

        // Verificar techo antes de intentar levantarse
        Vector3 crouchTop     = transform.position + Vector3.up * (crouchHeight / 2f);
        float neededClearance = (standingHeight - crouchHeight) + 0.1f;
        bool  hasCeiling      = Physics.Raycast(crouchTop, Vector3.up, neededClearance);

        if (hasCeiling)
        {
            // Sin espacio: quedarse agachado hasta que el jugador pulse C
            isCrouching = true;
        }
        else
        {
            // ORDEN CRÍTICO para evitar el bug de atravesar el suelo:
            // 1. center primero → recentra el pivot sin cambiar el tamaño del collider
            // 2. height después → el collider crece hacia arriba desde el nuevo pivot
            controller.center = standingCenter;
            controller.height = standingHeight;
            isCrouching = false;
        }

        // Limpiar velocity.y acumulada durante el slide
        if (controller.isGrounded)
            velocity.y = -2f;

        // Cooldown antes de poder volver a slidear
        yield return new WaitForSeconds(slideCooldown);
        canSlide = true;
    }

    // ─── Stamina ───────────────────────────────────────────────────────────────
    void HandleStamina()
    {
        if (isSprinting)
        {
            // Consumir stamina al correr
            currentStamina = Mathf.Max(0f, currentStamina - staminaDrainRate * Time.deltaTime);
        }
        else if (!isSliding)
        {
            // Regenerar stamina cuando no se usa
            currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRegenRate * Time.deltaTime);
        }
    }

    // ─── Gravedad ──────────────────────────────────────────────────────────────
    void ApplyGravity()
    {
        // Durante el slide la gravedad se aplica dentro de PerformSlide()
        // junto con el movimiento horizontal en un único controller.Move().
        // Aquí solo aplica cuando NO estamos slideando.
        if (isSliding) return;

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    // ─── API Pública ───────────────────────────────────────────────────────────

    /// <summary>
    /// Desactiva/activa todos los controles de movimiento.
    /// Llamar desde TerminalController al abrir/cerrar hackeo.
    /// </summary>
    public void SetMovementEnabled(bool enabled)
    {
        this.enabled = enabled;

        if (!enabled)
        {
            // Desbloquear cursor para la UI de terminal
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    /// <summary>
    /// Devuelve true si el jugador se está moviendo de forma audible.
    /// Útil para el sistema de detección de enemigos por sonido.
    /// Agacharse hace el movimiento silencioso según el GDD.
    /// </summary>
    public bool IsMovingAudibly()
    {
        if (isCrouching) return false;

        Vector3 horizontalVelocity = new Vector3(
            controller.velocity.x, 0f, controller.velocity.z);

        return horizontalVelocity.magnitude > 0.5f;
    }
}