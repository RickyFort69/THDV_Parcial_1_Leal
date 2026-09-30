using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyController : MonoBehaviour, IDamageable
{
    [Header("Movimiento y Detección")]
    [Tooltip("Velocidad de movimiento regulable desde el Inspector.")]
    [SerializeField] private float moveSpeed = 3.5f;
    [Tooltip("Distancia a la que el enemigo detecta y empieza a perseguir/atacar al jugador.")]
    [SerializeField] private float detectionRange = 5.0f;
    [Tooltip("Capa que representa al Jugador para el Raycast de ataque.")]
    [SerializeField] private LayerMask playerMask = ~0;

    [Header("Atributos del Enemigo")]
    [SerializeField] private float maxHealth = 100.0f;
    [SerializeField] private float currentHealth;

    [Header("Ataque (Pistola Semiautomática)")]
    [Tooltip("Rango máximo del disparo por Raycast.")]
    [SerializeField] private float attackRange = 5.0f;
    [Tooltip("Daño infligido al personaje jugable por cada disparo.")]
    [SerializeField] private float attackDamage = 20.0f;
    [Tooltip("Disparos por segundo.")]
    [SerializeField] private float fireRate = 2.0f;

    // Componentes y estado interno
    private Rigidbody rb;
    private PlayerController player;
    private bool isDead = false;
    private float nextTimeToFire = 0.0f;

    public bool IsDead => isDead;
    public float CurrentHealth => currentHealth;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        currentHealth = maxHealth;

        // Configuración de físicas necesaria para evitar que la cápsula se caiga
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void Start()
    {
        // Buscar al personaje jugable en la escena
        player = FindAnyObjectByType<PlayerController>();
    }

    private void Update()
    {
        // Si el enemigo está muerto o no encuentra al jugador, no hace nada
        if (isDead || player == null || player.IsDead) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);

        // Si el jugador está dentro de la distancia de 5m (regulable en el editor)
        if (distanceToPlayer <= detectionRange)
        {
            LookAtPlayer();
            TryAttack(distanceToPlayer);
        }
    }

    private void FixedUpdate()
    {
        // El movimiento físico va en FixedUpdate
        if (isDead || player == null || player.IsDead) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);

        // Perseguir solo si está dentro de la distancia establecida
        if (distanceToPlayer <= detectionRange)
        {
            MoveTowardsPlayer();
        }
        else
        {
            // Si sale del rango, se detiene el movimiento en seco
            Vector3 currentVel = rb.linearVelocity;
            currentVel.x = 0f;
            currentVel.z = 0f;
            rb.linearVelocity = currentVel;
        }
    }

    private void LookAtPlayer()
    {
        // Orientar la cápsula hacia la posición del jugador sin inclinarse en vertical (eje Y)
        Vector3 targetDirection = player.transform.position - transform.position;
        targetDirection.y = 0;

        if (targetDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
        }
    }

    private void MoveTowardsPlayer()
    {
        // Calcular dirección de persecución
        Vector3 direction = (player.transform.position - transform.position).normalized;
        direction.y = 0; // Mover solo en el plano horizontal

        Vector3 targetVelocity = direction * moveSpeed;
        targetVelocity.y = rb.linearVelocity.y; // Conservar la gravedad

        rb.linearVelocity = targetVelocity;
    }

    private void TryAttack(float distanceToPlayer)
    {
        // Verificar rango de ataque y cadencia
        if (distanceToPlayer > attackRange) return;
        if (Time.time < nextTimeToFire) return;

        // Siguiente tiempo disponible para disparar
        nextTimeToFire = Time.time + (1.0f / fireRate);

        // Disparo por Physics.Raycast desde el centro de la cápsula hacia el jugador
        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f; // Altura aproximada de ojos
        Vector3 rayDirection = (player.transform.position - transform.position).normalized;

        if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, attackRange, playerMask))
        {
            Debug.Log($"El enemigo disparó e impactó en: {hit.collider.name}");

            // Intentar hacer daño al jugador
            if (hit.collider.TryGetComponent<PlayerController>(out var playerTarget))
            {
                playerTarget.TakeDamage(attackDamage);
            }
        }
    }

    // Método proveniente de IDamageable para recibir disparos del Jugador
    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0.0f, maxHealth);

        Debug.Log($"Enemigo recibió daño. Vida restante: {currentHealth}");

        if (currentHealth <= 0.0f)
        {
            Die();
        }
    }

    private void Die()
    {
        isDead = true;

        // Frena el movimiento pero MANTIENE al enemigo en la escena sin destruirlo (Destroy no se llama)
        Vector3 currentVel = rb.linearVelocity;
        currentVel.x = 0f;
        currentVel.z = 0f;
        rb.linearVelocity = currentVel;

        Debug.Log("El enemigo ha muerto. Permanece en pantalla inmóvil e inofensivo.");
    }

    // Dibujar los rangos en el Editor para calibrar fácil
    private void OnDrawGizmosSelected()
    {
        // Rango de Detección/Persecución (Amarillo)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Rango de Ataque (Rojo)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}