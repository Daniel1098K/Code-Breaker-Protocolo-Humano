using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// BaseEnemy.cs - Code-Breaker: Protocolo Humano
/// Clase base para todos los enemigos. Heredar para crear Sentinel, Scanner, Enforcer y ProcessingUnit.
/// Requiere: NavMeshAgent, Collider adjuntos al GameObject.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Collider))]
public class BaseEnemy : MonoBehaviour, IDamageable
{
    // ─── Enums ─────────────────────────────────────────────────────────────────
    public enum EnemyState
    {
        Patrolling,
        Investigating,  // Oyó algo pero no ve al jugador
        Chasing,
        Attacking,
        Stunned,        // Sobrecarga de circuitos (blade cargado)
        Dead
    }

    public enum EnemyType { Sentinel, Scanner, Enforcer, ProcessingUnit }

    // ─── Configuración Base ────────────────────────────────────────────────────
    [Header("Identity")]
    public EnemyType enemyType = EnemyType.Sentinel;

    [Header("Stats")]
    [Tooltip("Salud máxima")]
    public float maxHealth = 100f;

    [Tooltip("Velocidad de movimiento")]
    public float moveSpeed = 3f;

    [Tooltip("Daño por ataque")]
    public float attackDamage = 10f;

    [Tooltip("Cooldown entre ataques en segundos")]
    public float attackCooldown = 1.5f;

    [Header("Detection")]
    [Tooltip("Radio de detección visual")]
    public float detectionRange = 10f;

    [Tooltip("Ángulo del cono de visión (grados totales)")]
    public float fieldOfView = 90f;

    [Tooltip("Radio de detección auditiva")]
    public float hearingRange = 6f;

    [Tooltip("Rango en que puede atacar al jugador")]
    public float attackRange = 5f;

    [Header("Patrol")]
    [Tooltip("Puntos de patrulla. Si está vacío, el enemigo queda estático.")]
    public Transform[] patrolPoints;

    [Tooltip("Tiempo de espera en cada punto de patrulla")]
    public float patrolWaitTime = 2f;

    [Header("VFX / Audio")]
    public ParticleSystem deathVFX;
    public ParticleSystem hitVFX;
    public ParticleSystem circuitOverloadVFX;
    public AudioSource audioSource;
    public AudioClip alertSound;
    public AudioClip attackSound;
    public AudioClip deathSound;

    // ─── Estado Interno ────────────────────────────────────────────────────────
    protected float currentHealth;
    protected EnemyState currentState = EnemyState.Patrolling;
    protected Transform player;
    protected NavMeshAgent agent;

    private int currentPatrolIndex = 0;
    private float patrolWaitTimer = 0f;
    private bool isWaitingAtPatrol = false;

    private float attackTimer = 0f;
    private float stunTimer = 0f;

    private Vector3 lastHeardPosition;  // Posición donde oyó al jugador
    private bool hasInvestigationTarget = false;

    // ─── Reprogramación (por hackeo del jugador) ───────────────────────────────
    private bool isReprogrammed = false;        // Atacar a sus aliados (Nivel 3)
    private string overrideTarget = "";         // "hostile_units" / "player"

    // ─── Propiedades Públicas ──────────────────────────────────────────────────
    public EnemyState CurrentState => currentState;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsAlive => currentState != EnemyState.Dead;
    public bool IsReprogrammed => isReprogrammed;

    // ──────────────────────────────────────────────────────────────────────────

    protected virtual void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;

        currentHealth = maxHealth;

        // Buscar jugador por tag
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;
    }

    protected virtual void Update()
    {
        if (currentState == EnemyState.Dead) return;

        attackTimer -= Time.deltaTime;

        switch (currentState)
        {
            case EnemyState.Patrolling:
                Patrol();
                CheckForPlayer();
                CheckForSound();
                break;

            case EnemyState.Investigating:
                Investigate();
                CheckForPlayer();
                break;

            case EnemyState.Chasing:
                ChasePlayer();
                CheckAttackRange();
                break;

            case EnemyState.Attacking:
                FacePlayer();
                PerformAttack();
                CheckChaseNeeded();
                break;

            case EnemyState.Stunned:
                HandleStun();
                break;
        }
    }

    // ─── PATRULLA ──────────────────────────────────────────────────────────────
    void Patrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;

        if (isWaitingAtPatrol)
        {
            patrolWaitTimer -= Time.deltaTime;
            if (patrolWaitTimer <= 0f)
            {
                isWaitingAtPatrol = false;
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
                agent.SetDestination(patrolPoints[currentPatrolIndex].position);
            }
            return;
        }

        // Llegó al punto de patrulla
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            isWaitingAtPatrol = true;
            patrolWaitTimer = patrolWaitTime;
        }
    }

    // ─── DETECCIÓN ─────────────────────────────────────────────────────────────
    void CheckForPlayer()
    {
        if (player == null) return;

        float distToPlayer = Vector3.Distance(transform.position, player.position);

        // ── Detección visual (cono de visión + raycast) ──
        if (distToPlayer <= detectionRange)
        {
            Vector3 dirToPlayer = (player.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, dirToPlayer);

            if (angle <= fieldOfView / 2f)
            {
                // Raycast para verificar línea de visión (sin obstáculos)
                if (Physics.Raycast(transform.position + Vector3.up * 0.5f,
                    dirToPlayer, out RaycastHit hit, detectionRange))
                {
                    if (hit.transform == player || hit.transform.IsChildOf(player))
                    {
                        TransitionToState(EnemyState.Chasing);
                        AlertNearbyEnemies(); // Avisar a enemigos cercanos
                        return;
                    }
                }
            }
        }

        // ── Detección auditiva ──
        if (distToPlayer <= hearingRange)
        {
            PlayerMovement pm = player.GetComponent<PlayerMovement>();
            if (pm != null && pm.IsMovingAudibly())
            {
                lastHeardPosition = player.position;
                hasInvestigationTarget = true;
                if (currentState == EnemyState.Patrolling)
                    TransitionToState(EnemyState.Investigating);
            }
        }
    }

    void CheckForSound()
    {
        // Separado para poder llamarlo solo en estados relevantes
        CheckForPlayer();
    }

    // ─── INVESTIGACIÓN ─────────────────────────────────────────────────────────
    void Investigate()
    {
        if (!hasInvestigationTarget)
        {
            TransitionToState(EnemyState.Patrolling);
            return;
        }

        agent.SetDestination(lastHeardPosition);

        // Llegó al punto donde oyó el ruido
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f)
        {
            hasInvestigationTarget = false;
            TransitionToState(EnemyState.Patrolling);
        }
    }

    // ─── PERSECUCIÓN ───────────────────────────────────────────────────────────
    void ChasePlayer()
    {
        if (player == null) return;

        // Si está reprogramado, perseguir a un aliado en cambio
        if (isReprogrammed)
        {
            ChaseNearestAlly();
            return;
        }

        agent.SetDestination(player.position);

        // Si pierde de vista al jugador durante más de 3 segundos, investigar
        float distToPlayer = Vector3.Distance(transform.position, player.position);
        if (distToPlayer > detectionRange * 1.5f)
        {
            lastHeardPosition = player.position;
            hasInvestigationTarget = true;
            TransitionToState(EnemyState.Investigating);
        }
    }

    void CheckAttackRange()
    {
        if (player == null) return;

        float dist = Vector3.Distance(transform.position, player.position);
        if (dist <= attackRange)
            TransitionToState(EnemyState.Attacking);
    }

    void CheckChaseNeeded()
    {
        if (player == null) return;

        float dist = Vector3.Distance(transform.position, player.position);
        if (dist > attackRange * 1.2f)
            TransitionToState(EnemyState.Chasing);
    }

    // ─── ATAQUE ────────────────────────────────────────────────────────────────
    void PerformAttack()
    {
        if (attackTimer > 0f) return;

        attackTimer = attackCooldown;
        OnAttack(); // Las subclases definen cómo atacan
    }

    /// <summary>
    /// Override en subclases para definir el tipo de ataque (ranged, melee, etc.)
    /// </summary>
    protected virtual void OnAttack()
    {
        if (player == null) return;

        float dist = Vector3.Distance(transform.position, player.position);
        if (dist > attackRange) return;

        // Aplicar daño via IDamageable
        IDamageable damageable = player.GetComponent<IDamageable>();
        if (damageable != null) damageable.TakeDamage(attackDamage);

        PlaySound(attackSound);
    }

    void FacePlayer()
    {
        if (player == null) return;
        Vector3 dir = (player.position - transform.position).normalized;
        dir.y = 0f;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(dir), Time.deltaTime * 5f);
    }

    // ─── DAÑO Y MUERTE ─────────────────────────────────────────────────────────

    /// <summary>
    /// Recibe daño. Llamado desde PlayerShooting.
    /// </summary>
    public virtual void TakeDamage(float damage)
    {
        if (currentState == EnemyState.Dead) return;

        currentHealth -= damage;

        // VFX de impacto
        if (hitVFX != null) hitVFX.Play();

        // Si estaba patrullando, alerta
        if (currentState == EnemyState.Patrolling)
            TransitionToState(EnemyState.Chasing);

        if (currentHealth <= 0f)
            Die();
    }

    /// <summary>
    /// Sobrecarga de circuitos: aturde al enemigo (ataque cargado de la Blade).
    /// </summary>
    public virtual void TriggerCircuitOverload()
    {
        if (currentState == EnemyState.Dead) return;

        stunTimer = 3f; // 3 segundos de aturdimiento
        TransitionToState(EnemyState.Stunned);

        if (circuitOverloadVFX != null) circuitOverloadVFX.Play();
        agent.isStopped = true;
    }

    void HandleStun()
    {
        stunTimer -= Time.deltaTime;
        if (stunTimer <= 0f)
        {
            agent.isStopped = false;
            TransitionToState(EnemyState.Chasing);
        }
    }

    protected virtual void Die()
    {
        currentState = EnemyState.Dead;
        agent.isStopped = true;
        agent.enabled = false;

        // Desactivar collider para que no bloquee
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        PlaySound(deathSound);
        if (deathVFX != null) deathVFX.Play();

        OnDeath(); // Hook para subclases (drops, eventos, etc.)

        Destroy(gameObject, 2f);
    }

    /// <summary>
    /// Hook para lógica de muerte específica de cada enemigo (drops, eventos, etc.)
    /// </summary>
    protected virtual void OnDeath() { }

    // ─── HACKEO / REPROGRAMACIÓN ───────────────────────────────────────────────

    /// <summary>
    /// Reprograma al enemigo para atacar a sus aliados.
    /// Llamado desde TerminalController en el Nivel 3.
    /// </summary>
    public void Reprogram(string targetOverride)
    {
        isReprogrammed = true;
        overrideTarget = targetOverride;
        TransitionToState(EnemyState.Chasing);
    }

    void ChaseNearestAlly()
    {
        // Buscar el enemigo más cercano (excluyendo al propio)
        BaseEnemy[] allEnemies = FindObjectsByType<BaseEnemy>(FindObjectsSortMode.None);
        BaseEnemy nearest = null;
        float nearestDist = float.MaxValue;

        foreach (BaseEnemy e in allEnemies)
        {
            if (e == this || !e.IsAlive || e.IsReprogrammed) continue;

            float dist = Vector3.Distance(transform.position, e.transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = e;
            }
        }

        if (nearest != null)
        {
            agent.SetDestination(nearest.transform.position);

            // Atacar al aliado si está en rango
            if (nearestDist <= attackRange && attackTimer <= 0f)
            {
                nearest.TakeDamage(attackDamage);
                attackTimer = attackCooldown;
            }
        }
    }

    // ─── ALERTAS COOPERATIVAS ──────────────────────────────────────────────────

    /// <summary>
    /// Alerta a todos los enemigos en un radio de 15 metros.
    /// </summary>
    void AlertNearbyEnemies()
    {
        Collider[] nearby = Physics.OverlapSphere(transform.position, 15f);
        foreach (Collider col in nearby)
        {
            BaseEnemy other = col.GetComponent<BaseEnemy>();
            if (other != null && other != this && other.IsAlive)
                other.ReceiveAlert(player.position);
        }
    }

    /// <summary>
    /// Recibe alerta de un compañero: transiciona a persecución.
    /// </summary>
    public void ReceiveAlert(Vector3 playerPosition)
    {
        if (currentState == EnemyState.Dead || currentState == EnemyState.Chasing) return;

        lastHeardPosition = playerPosition;
        hasInvestigationTarget = true;
        TransitionToState(EnemyState.Investigating);
    }

    // ─── UTILIDADES ────────────────────────────────────────────────────────────
    protected void TransitionToState(EnemyState newState)
    {
        if (currentState == newState) return;
        currentState = newState;

        switch (newState)
        {
            case EnemyState.Chasing:
                agent.isStopped = false;
                PlaySound(alertSound);
                break;

            case EnemyState.Attacking:
                agent.isStopped = true;
                break;

            case EnemyState.Patrolling:
                agent.isStopped = false;
                if (patrolPoints != null && patrolPoints.Length > 0)
                    agent.SetDestination(patrolPoints[currentPatrolIndex].position);
                break;
        }
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    // ─── GIZMOS (Editor) ───────────────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        // Radio de detección visual
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Radio de detección auditiva
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, hearingRange);

        // Radio de ataque
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Cono de visión
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Vector3 leftBound = Quaternion.Euler(0, -fieldOfView / 2f, 0) * transform.forward;
        Vector3 rightBound = Quaternion.Euler(0, fieldOfView / 2f, 0) * transform.forward;
        Gizmos.DrawRay(transform.position, leftBound * detectionRange);
        Gizmos.DrawRay(transform.position, rightBound * detectionRange);
    }
}