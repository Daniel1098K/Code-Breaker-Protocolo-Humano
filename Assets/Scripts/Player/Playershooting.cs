using System.Collections;
using UnityEngine;

/// <summary>
/// PlayerShooting.cs - Code-Breaker: Protocolo Humano
/// Gestiona todas las armas del jugador: Cuchilla Térmica "Byte", Pistola Ping y Rifle Kernel.
/// Requiere: PlayerMovement adjunto al mismo GameObject, cámara FPS asignada.
/// </summary>
public class PlayerShooting : MonoBehaviour
{
    // ─── Singleton (acceso desde TerminalController) ───────────────────────────
    public static PlayerShooting Instance { get; private set; }

    // ─── Enums ─────────────────────────────────────────────────────────────────
    public enum WeaponType { ThermalBlade, PingPistol, KernelRifle }
    public enum FireMode { Automatic, SemiAutomatic }

    // ─── Referencias ───────────────────────────────────────────────────────────
    [Header("References")]
    [Tooltip("Cámara FPS (origen de raycasts)")]
    public Camera playerCamera;

    [Tooltip("Punto de spawn de proyectiles (vacío hijo de la cámara)")]
    public Transform muzzlePoint;

    [Tooltip("Script de retroceso adjunto al arma")]
    public WeaponRecoil weaponRecoil;

    [Tooltip("Controlador de animaciones del arma activa")]
    public WeaponAnimator weaponAnimator;

    [Tooltip("HUD: imagen de la retícula (RectTransform)")]
    public RectTransform crosshair;

    [Header("Weapon Models")]
    [Tooltip("GameObject del modelo de la Cuchilla Térmica en escena")]
    public GameObject bladeModel;

    [Tooltip("GameObject del modelo de la Pistola Ping en escena")]
    public GameObject pingModel;

    [Tooltip("GameObject del modelo del Rifle Kernel en escena")]
    public GameObject kernelModel;

    // ─── Arma Actual ───────────────────────────────────────────────────────────
    [Header("Current Weapon")]
    public WeaponType currentWeapon = WeaponType.PingPistol;

    // ══════════════════════════════════════════════════════════════════════════
    // 1. CUCHILLA TÉRMICA "BYTE"
    // ══════════════════════════════════════════════════════════════════════════
    [Header("─── Thermal Blade 'Byte' ───")]
    [Tooltip("Daño por golpe")]
    public float bladeDamage = 50f;

    [Tooltip("Ataques por segundo")]
    public float bladeAttackRate = 2f;           // → cooldown 0.5s entre ataques

    [Tooltip("Alcance del ataque cuerpo a cuerpo (metros)")]
    public float bladeRange = 2f;

    [Tooltip("Daño del ataque cargado (sobrecarga de circuitos)")]
    public float bladeChargedDamage = 150f;

    [Tooltip("Tiempo mínimo manteniendo para activar ataque cargado")]
    public float bladeChargeTime = 1f;

    [Tooltip("VFX de trail al atacar")]
    public ParticleSystem bladeTrailVFX;

    [Tooltip("VFX de sobrecarga al atacar cargado")]
    public ParticleSystem bladeChargeVFX;

    // ══════════════════════════════════════════════════════════════════════════
    // 2. PISTOLA DE PULSOS "PING"
    // ══════════════════════════════════════════════════════════════════════════
    [Header("─── Pulse Pistol 'Ping' ───")]
    [Tooltip("Daño por disparo")]
    public float pingDamage = 25f;

    [Tooltip("Disparos por segundo")]
    public float pingFireRate = 3f;

    [Tooltip("Distancia máxima de impacto (raycast)")]
    public float pingRange = 100f;

    // Sin munición: recarga desde batería corporal (infinita)

    // ══════════════════════════════════════════════════════════════════════════
    // 3. RIFLE DE ASALTO "KERNEL"
    // ══════════════════════════════════════════════════════════════════════════
    [Header("─── Assault Rifle 'Kernel' ───")]
    [Tooltip("Daño por disparo")]
    public float kernelDamage = 35f;

    [Tooltip("Disparos por segundo")]
    public float kernelFireRate = 8f;

    [Tooltip("Tamaño del cargador")]
    public int kernelMagazineSize = 30;

    [Tooltip("Munición de reserva máxima")]
    public int kernelMaxReserve = 120;

    [Tooltip("Tiempo de recarga en segundos")]
    public float kernelReloadTime = 2f;

    [Tooltip("Prefab del proyectil rojo")]
    public GameObject kernelProjectilePrefab;

    [Tooltip("Velocidad del proyectil")]
    public float kernelProjectileSpeed = 60f;

    [Tooltip("Distancia máxima de impacto")]
    public float kernelRange = 200f;

    [Tooltip("Duración del mod de Sobrecarga (daño x2)")]
    public float kernelOverloadDuration = 5f;

    [Tooltip("Cooldown del mod de Sobrecarga")]
    public float kernelOverloadCooldown = 30f;

    [Tooltip("VFX de Sobrecarga activa")]
    public ParticleSystem kernelOverloadVFX;

    [Header("─── Laser VFX ───")]
    [Tooltip("Prefab LaserShot_Prefab con LaserProjectile.cs adjunto")]
    public GameObject pingProjectilePrefab;
    // Blade
    private float bladeNextAttackTime = 0f;
    private bool isChargingBlade = false;
    private float bladeChargeStartTime = 0f;

    // Ping
    private float pingNextFireTime = 0f;

    // Kernel
    private int kernelCurrentAmmo;
    private int kernelCurrentReserve;
    private bool kernelIsReloading = false;
    private bool kernelOverloadActive = false;
    private float kernelOverloadNextUse = 0f;
    private FireMode kernelFireMode = FireMode.Automatic;

    // General
    private bool canShoot = true;    // false durante hackeo

    // ─── Propiedades Públicas ──────────────────────────────────────────────────
    public int KernelCurrentAmmo => kernelCurrentAmmo;
    public int KernelCurrentReserve => kernelCurrentReserve;
    public bool KernelIsReloading => kernelIsReloading;
    public bool KernelOverloadActive => kernelOverloadActive;
    public float KernelOverloadCooldownRemaining => Mathf.Max(0f, kernelOverloadNextUse - Time.time);
    public WeaponType CurrentWeapon => currentWeapon;

    // ──────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        kernelCurrentAmmo    = kernelMagazineSize;
        kernelCurrentReserve = kernelMaxReserve;

        // Inicializar visibilidad de modelos según arma por defecto
        SwitchWeapon(currentWeapon);
    }

    void Update()
    {
        if (!canShoot) return;

        HandleWeaponSwitch();

        switch (currentWeapon)
        {
            case WeaponType.ThermalBlade:  HandleBlade();       break;
            case WeaponType.PingPistol:    HandlePing();        break;
            case WeaponType.KernelRifle:   HandleKernel();      break;
        }

        UpdateCrosshair();
        HandleUtilityInput();
    }

    // ─── Input Global (independiente del arma equipada) ───────────────────────
    //
    //  R  → Recarga manual (solo Kernel, si hay reserva y cargador incompleto)
    //  F  → Inspección del arma (solo si no está disparando ni recargando)
    //  Q  → Mod Sobrecarga del Kernel (daño x2 durante 5 seg, cooldown 30 seg)
    //
    void HandleUtilityInput()
    {
        // ── R: Recarga manual ──────────────────────────────────────────────────
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (currentWeapon == WeaponType.KernelRifle
                && !kernelIsReloading
                && kernelCurrentAmmo < kernelMagazineSize
                && kernelCurrentReserve > 0)
            {
                StartCoroutine(ReloadKernel());
            }
            // Ping: munición infinita → sin recarga
            // Blade: no usa munición → sin recarga
        }

        // ── F: Inspección del arma ─────────────────────────────────────────────
        if (Input.GetKeyDown(KeyCode.F))
        {
            bool isBusy = kernelIsReloading
                       || Input.GetMouseButton(0)
                       || (currentWeapon == WeaponType.ThermalBlade && isChargingBlade);

            if (!isBusy && weaponAnimator != null)
                weaponAnimator.PlayInspect();
        }
    }

    // ─── Cambio de Arma ────────────────────────────────────────────────────────
    void HandleWeaponSwitch()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchWeapon(WeaponType.ThermalBlade);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchWeapon(WeaponType.PingPistol);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchWeapon(WeaponType.KernelRifle);

        // Scroll del ratón
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll > 0f) CycleWeapon(1);
        if (scroll < 0f) CycleWeapon(-1);
    }

    void SwitchWeapon(WeaponType weapon)
    {
        // Cancelar recarga si se cambia de arma
        if (kernelIsReloading) StopAllCoroutines();
        kernelIsReloading = false;
        isChargingBlade = false;

        currentWeapon = weapon;

        // Activar solo el modelo del arma seleccionada
        if (bladeModel  != null) bladeModel.SetActive(weapon == WeaponType.ThermalBlade);
        if (pingModel   != null) pingModel.SetActive(weapon  == WeaponType.PingPistol);
        if (kernelModel != null) kernelModel.SetActive(weapon == WeaponType.KernelRifle);

        // Reasignar WeaponRecoil y WeaponAnimator al modelo activo
        if (weapon == WeaponType.ThermalBlade)
        {
            weaponRecoil   = bladeModel  != null ? bladeModel.GetComponent<WeaponRecoil>()   : null;
            weaponAnimator = bladeModel  != null ? bladeModel.GetComponent<WeaponAnimator>() : null;
        }
        else if (weapon == WeaponType.PingPistol)
        {
            weaponRecoil   = pingModel   != null ? pingModel.GetComponent<WeaponRecoil>()    : null;
            weaponAnimator = pingModel   != null ? pingModel.GetComponent<WeaponAnimator>()  : null;
        }
        else if (weapon == WeaponType.KernelRifle)
        {
            weaponRecoil   = kernelModel != null ? kernelModel.GetComponent<WeaponRecoil>()   : null;
            weaponAnimator = kernelModel != null ? kernelModel.GetComponent<WeaponAnimator>() : null;
        }

        // Cancelar animación activa (inspect, etc.) y forzar Idle en el nuevo modelo
        if (weaponAnimator != null) weaponAnimator.ForceIdle();

        // Notificar al sistema ADS del cambio de arma
        if (WeaponADS.Instance != null) WeaponADS.Instance.SetWeapon(weapon);
    }

    void CycleWeapon(int direction)
    {
        int count = System.Enum.GetValues(typeof(WeaponType)).Length;
        int next = ((int)currentWeapon + direction + count) % count;
        SwitchWeapon((WeaponType)next);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // BLADE — Cuchilla Térmica "Byte"
    // ══════════════════════════════════════════════════════════════════════════
    void HandleBlade()
    {
        // Iniciar carga al mantener botón
        if (Input.GetMouseButtonDown(0))
        {
            bladeChargeStartTime = Time.time;
            isChargingBlade = true;
        }

        // Soltar botón: decidir si es ataque normal o cargado
        if (Input.GetMouseButtonUp(0) && isChargingBlade)
        {
            isChargingBlade = false;
            float heldTime = Time.time - bladeChargeStartTime;

            if (Time.time >= bladeNextAttackTime)
            {
                if (heldTime >= bladeChargeTime)
                    PerformBladeAttack(charged: true);
                else
                    PerformBladeAttack(charged: false);

                bladeNextAttackTime = Time.time + (1f / bladeAttackRate);
            }
        }
    }

    void PerformBladeAttack(bool charged)
    {
        float damage = charged ? bladeChargedDamage : bladeDamage;

        // VFX
        if (charged && bladeChargeVFX != null) bladeChargeVFX.Play();
        else if (bladeTrailVFX != null) bladeTrailVFX.Play();

        // Raycast de cuerpo a cuerpo
        Ray ray = playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f));
        if (Physics.Raycast(ray, out RaycastHit hit, bladeRange))
        {
            BaseEnemy enemy = hit.collider.GetComponent<BaseEnemy>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage);

                // Ejecución silenciosa: el ataque cargado sobrecarga circuitos
                if (charged)
                    enemy.TriggerCircuitOverload();
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PING — Pistola de Pulsos
    // ══════════════════════════════════════════════════════════════════════════
    void HandlePing()
    {
        // Semi-automática: un clic = un disparo
        if (Input.GetMouseButtonDown(0) && Time.time >= pingNextFireTime)
        {
            FirePing();
            pingNextFireTime = Time.time + (1f / pingFireRate);
        }
    }

    void FirePing()
    {
        if (weaponRecoil   != null) weaponRecoil.TriggerRecoil();
        if (weaponAnimator != null) weaponAnimator.PlayShoot();

        if (muzzlePoint == null || playerCamera == null) return;

        // Calcular punto de impacto desde el centro de la cámara
        Ray ray = playerCamera.ScreenPointToRay(
            new Vector3(Screen.width / 2f, Screen.height / 2f));

        Vector3 targetPoint = ray.origin + ray.direction * pingRange;
        if (Physics.Raycast(ray, out RaycastHit hit, pingRange))
        {
            targetPoint = hit.point;

            // Daño aplicado aquí (hitscan) — el proyectil es solo visual
            IDamageable damageable = hit.collider.GetComponent<IDamageable>();
            if (damageable != null)
                damageable.TakeDamage(pingDamage);
        }

        // Instanciar proyectil visual y lanzarlo desde el muzzle hacia targetPoint
        if (pingProjectilePrefab != null)
        {
            GameObject proj = Instantiate(pingProjectilePrefab);
            LaserProjectile laser = proj.GetComponent<LaserProjectile>();
            if (laser != null)
                laser.Launch(muzzlePoint.position, targetPoint, pingDamage);
            else
                Destroy(proj, 2f);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // KERNEL — Rifle de Asalto (Ráfaga 3 disparos / Semi-auto en ADS)
    // ══════════════════════════════════════════════════════════════════════════
    [Header("─── Burst Settings (Kernel) ───")]
    [Tooltip("Número de disparos por ráfaga")]
    public int burstCount = 3;

    [Tooltip("Tiempo entre disparos dentro de la misma ráfaga")]
    public float burstFireRate = 0.08f; // ~12 disparos/seg dentro de la ráfaga

    private bool isBursting = false;    // true mientras ejecuta una ráfaga
    void HandleKernel()
    {
        if (kernelIsReloading || isBursting) return;

        // RMB: entrar/salir de ADS (semi-auto, un disparo por clic)
        if (Input.GetMouseButtonDown(1))
            kernelFireMode = FireMode.SemiAutomatic;
        if (Input.GetMouseButtonUp(1))
            kernelFireMode = FireMode.Automatic;

        // Disparo
        // - Hip-fire (Automático): ráfaga de burstCount disparos por clic
        // - ADS (Semi-auto):       un disparo por clic, sin ráfaga
        bool triggerPressed = Input.GetMouseButtonDown(0);

        if (triggerPressed && Time.time >= pingNextFireTime && kernelCurrentAmmo > 0)
        {
            if (kernelFireMode == FireMode.Automatic)
                StartCoroutine(FireBurst());
            else
                FireKernelSingle(); // ADS: disparo único preciso

            pingNextFireTime = Time.time + (1f / kernelFireRate);
        }

        // Auto-recarga al vaciar cargador
        if (kernelCurrentAmmo <= 0 && kernelCurrentReserve > 0 && !kernelIsReloading)
            StartCoroutine(ReloadKernel());

        // Mod Sobrecarga: tecla Q
        if (Input.GetKeyDown(KeyCode.Q) && !kernelOverloadActive && Time.time >= kernelOverloadNextUse)
            StartCoroutine(ActivateKernelOverload());
    }

    IEnumerator FireBurst()
    {
        isBursting = true;

        int shotsToFire = Mathf.Min(burstCount, kernelCurrentAmmo);

        for (int i = 0; i < shotsToFire; i++)
        {
            FireKernelSingle(hipfireSpread: true);
            yield return new WaitForSeconds(burstFireRate);
        }

        isBursting = false;
    }

    void FireKernelSingle(bool hipfireSpread = false)
    {
        if (kernelCurrentAmmo <= 0) return;

        kernelCurrentAmmo--;
        if (weaponAnimator != null) weaponAnimator.PlayShoot();
        if (weaponRecoil   != null) weaponRecoil.TriggerRecoil();

        float damage = kernelOverloadActive ? kernelDamage * 2f : kernelDamage;

        Ray ray = playerCamera.ScreenPointToRay(
            new Vector3(Screen.width / 2f, Screen.height / 2f));

        // Spread solo en hip-fire; ADS es perfectamente preciso
        if (hipfireSpread)
        {
            float spread = 0.025f;
            ray = new Ray(ray.origin, ray.direction + new Vector3(
                Random.Range(-spread, spread),
                Random.Range(-spread, spread),
                0f));
        }

        Vector3 targetPoint = ray.origin + ray.direction * kernelRange;
        if (Physics.Raycast(ray, out RaycastHit hit, kernelRange))
        {
            targetPoint = hit.point;
            IDamageable damageable = hit.collider.GetComponent<IDamageable>();
            if (damageable != null)
                damageable.TakeDamage(damage);
            // TODO: Spawn VFX de impacto (marcas hexagonales en pared)
        }

        // Proyectil visual rojo
        if (kernelProjectilePrefab != null && muzzlePoint != null)
        {
            GameObject proj = Instantiate(
                kernelProjectilePrefab, muzzlePoint.position, muzzlePoint.rotation);
            Rigidbody rb = proj.GetComponent<Rigidbody>();
            if (rb != null)
                rb.velocity = (targetPoint - muzzlePoint.position).normalized
                              * kernelProjectileSpeed;
            Destroy(proj, 2f);
        }
    }

    IEnumerator ReloadKernel()
    {
        kernelIsReloading = true;
        if (weaponAnimator != null) weaponAnimator.PlayReload();
        // TODO: Reproducir animación de recarga

        yield return new WaitForSeconds(kernelReloadTime);

        int needed = kernelMagazineSize - kernelCurrentAmmo;
        int toReload = Mathf.Min(needed, kernelCurrentReserve);
        kernelCurrentAmmo += toReload;
        kernelCurrentReserve -= toReload;

        kernelIsReloading = false;
    }

    IEnumerator ActivateKernelOverload()
    {
        kernelOverloadActive = true;
        kernelOverloadNextUse = Time.time + kernelOverloadCooldown;

        if (kernelOverloadVFX != null) kernelOverloadVFX.Play();

        yield return new WaitForSeconds(kernelOverloadDuration);

        kernelOverloadActive = false;
        if (kernelOverloadVFX != null) kernelOverloadVFX.Stop();
    }

    // ─── Retícula Dinámica ─────────────────────────────────────────────────────
    void UpdateCrosshair()
    {
        if (crosshair == null) return;

        // Se expande mientras se dispara en automático
        bool shooting = Input.GetMouseButton(0) && currentWeapon == WeaponType.KernelRifle;
        float targetSize = shooting ? 60f : 30f;
        crosshair.sizeDelta = Vector2.Lerp(crosshair.sizeDelta, Vector2.one * targetSize, Time.deltaTime * 10f);
    }

    // ─── API Pública ───────────────────────────────────────────────────────────

    /// <summary>
    /// Activa/desactiva disparos. Llamar desde TerminalController al hackear.
    /// </summary>
    public void SetShootingEnabled(bool enabled)
    {
        canShoot = enabled;

        // Al abrir terminal: salir de ADS inmediatamente
        if (!enabled && WeaponADS.Instance != null)
            WeaponADS.Instance.ForceHip();
    }

    /// <summary>
    /// Añade munición al rifle (pickup en nivel).
    /// </summary>
    public void AddKernelAmmo(int amount)
    {
        kernelCurrentReserve = Mathf.Min(kernelCurrentReserve + amount, kernelMaxReserve);
    }

    /// <summary>
    /// Activa munición ilimitada para el Nivel 3 (mecánica de oleadas).
    /// </summary>
    public void SetInfiniteAmmo(bool infinite)
    {
        // Si infinite=true, la reserva se mantiene siempre llena
        StopCoroutine(nameof(InfiniteAmmoLoop));
        if (infinite) StartCoroutine(InfiniteAmmoLoop());
    }

    IEnumerator InfiniteAmmoLoop()
    {
        while (true)
        {
            kernelCurrentReserve = kernelMaxReserve;
            yield return new WaitForSeconds(0.5f);
        }
    }
}