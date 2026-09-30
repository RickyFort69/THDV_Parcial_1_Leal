using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movimiento")]
    [Tooltip("Velocidad de movimiento regulable desde el Inspector.")]
    [SerializeField] private float moveSpeed = 6.0f;

    [Header("Salto y Físicas de Suelo")]
    [Tooltip("Fuerza del salto regulable desde el Inspector.")]
    [SerializeField] private float jumpForce = 5.0f;
    [Tooltip("Capa que el personaje considera como suelo para poder saltar.")]
    [SerializeField] private LayerMask groundMask;
    [Tooltip("Transform ubicado en los pies del personaje para detectar el suelo.")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundDistance = 0.3f;

    [Header("Cámara y FOV")]
    [SerializeField] private Camera playerCamera;
    [Range(0.01f, 1.0f)]
    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float minFOV = 50.0f;
    [SerializeField] private float maxFOV = 120.0f;
    [SerializeField] private float fovChangeSpeed = 40.0f;

    [Header("Inventario")]
    [Tooltip("Array para almacenar cada moneda en una posición única.")]
    [SerializeField] private int[] coinInventory = new int[5]; // Array de 5 posiciones
    private int currentCoinIndex = 0; // Índice de la siguiente posición libre

    public int[] CoinInventory => coinInventory;

    // Método para agregar la moneda al array
    public void AddCoin(int coinValue)
    {
        if (currentCoinIndex < coinInventory.Length)
        {
            // Se guarda en una posición única del array
            coinInventory[currentCoinIndex] = coinValue;
            currentCoinIndex++;
            Debug.Log($"Moneda guardada en la posición [{currentCoinIndex - 1}] del array.");
        }
        else
        {
            Debug.Log("El inventario de monedas está lleno.");
        }
    }

    // Método para mostrar el inventario en consola al presionar 'I'
    private void HandleInventoryInput()
    {
        if (Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame)
        {
            int totalCoins = GetTotalCoins();
            Debug.Log($"MONEDAS TOTALES EN INVENTARIO: {totalCoins}");
        }
    }

    // Calcula el total sumando los valores almacenados en el array
    private int GetTotalCoins()
    {
        int total = 0;
        for (int i = 0; i < coinInventory.Length; i++)
        {
            total += coinInventory[i];
        }
        return total;
    }

    [Header("Atributos del Jugador")]
    [SerializeField] private float maxHealth = 100.0f;
    [SerializeField] private float currentHealth;
    [SerializeField] private float maxStamina = 10.0f;
    [SerializeField] private float currentStamina;
    [SerializeField] private float staminaRegenRate = 2.0f; // 2 puntos por segundo
    [SerializeField] private float jumpStaminaCost = 5.0f;  // Cada salto gasta 5

    [Header("Ataque / Arma (Pistola)")]
    [Tooltip("Distancia máxima del disparo.")]
    [SerializeField] private float attackRange = 20.0f;
    [Tooltip("Daño infligido por disparo.")]
    [SerializeField] private float attackDamage = 25.0f;
    [Tooltip("Disparos por segundo.")]
    [SerializeField] private float fireRate = 1.5f;
    [Tooltip("Cantidad total/reserva de balas.")]
    [SerializeField] private int ammoCount = 10;
    [Tooltip("Capas de objetos que pueden recibir disparos.")]
    [SerializeField] private LayerMask attackMask = ~0; // Todo por defecto

    // Componentes y estado interno
    private Rigidbody rb;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private float cameraPitch = 0.0f;
    private bool isGrounded = false;
    private bool jumpRequested = false;
    private bool isDead = false;
    private float nextTimeToFire = 0.0f;

    public bool IsDead => isDead;
    public float CurrentHealth => currentHealth;
    public float CurrentStamina => currentStamina;
    public bool IsGrounded => isGrounded;
    public int AmmoCount => ammoCount;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        currentHealth = maxHealth;
        currentStamina = maxStamina;

        // Auto-asignar cámara si falta
        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }

        // Configuración física
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Ocultar y capturar el ratón
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (isDead) return;

        CheckGrounded();
        RegenerateStamina();
        ReadInputs();
        HandleLook();
        HandleFOV();
        HandleInventoryInput();
        
    }

    private void FixedUpdate()
    {
        if (isDead) return;

        HandleMovement();

        if (jumpRequested)
        {
            PerformJump();
            jumpRequested = false;
        }
    }

    private void CheckGrounded()
    {
        if (groundCheck != null)
        {
            isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
        }
        else
        {
            isGrounded = Physics.Raycast(transform.position, Vector3.down, 1.1f);
        }
    }

    private void RegenerateStamina()
    {
        if (currentStamina < maxStamina)
        {
            currentStamina += staminaRegenRate * Time.deltaTime;
            currentStamina = Mathf.Clamp(currentStamina, 0.0f, maxStamina);
        }
    }

    private void ReadInputs()
    {
        if (Keyboard.current != null)
        {
            Vector2 rawMove = Vector2.zero;
            if (Keyboard.current.wKey.isPressed) rawMove.y += 1f;
            if (Keyboard.current.sKey.isPressed) rawMove.y -= 1f;
            if (Keyboard.current.dKey.isPressed) rawMove.x += 1f;
            if (Keyboard.current.aKey.isPressed) rawMove.x -= 1f;

            moveInput = rawMove.normalized;

            // Salto
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                if (isGrounded && currentStamina >= jumpStaminaCost)
                {
                    jumpRequested = true;
                }
            }
        }

        if (Mouse.current != null)
        {
            lookInput = Mouse.current.delta.ReadValue() * mouseSensitivity;

            // Ataque semiautomático (Click izquierdo del ratón - una pulsación por disparo)
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                TryShoot();
            }
        }
    }

    private void TryShoot()
    {
        // Verificar cadencia de tiro y munición disponible
        if (Time.time < nextTimeToFire) return;
        if (ammoCount <= 0)
        {
            Debug.Log("¡Sin munición!");
            return;
        }

        // Calcular siguiente tiempo habilitado para disparar según la cadencia
        nextTimeToFire = Time.time + (1.0f / fireRate);

        // Descontar una bala
        ammoCount--;
        Debug.Log($"Pum! Balas restantes: {ammoCount}");

        // Raycast lanzado exactamente desde el centro del Viewport de la Cámara (0.5, 0.5)
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, attackRange, attackMask))
        {
            Debug.Log($"Impacto en: {hit.collider.name}");

            // Intentar aplicar daño si el objeto implementa IDamageable
            if (hit.collider.TryGetComponent<IDamageable>(out var damageable))
            {
                damageable.TakeDamage(attackDamage);
            }
        }
    }

    private void HandleMovement()
    {
        Vector3 moveDirection = transform.right * moveInput.x + transform.forward * moveInput.y;
        Vector3 targetVelocity = moveDirection * moveSpeed;
        targetVelocity.y = rb.linearVelocity.y;

        rb.linearVelocity = targetVelocity;
    }

    private void PerformJump()
    {
        currentStamina -= jumpStaminaCost;
        currentStamina = Mathf.Clamp(currentStamina, 0.0f, maxStamina);

        Vector3 currentVel = rb.linearVelocity;
        currentVel.y = 0f;
        rb.linearVelocity = currentVel;

        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    private void HandleLook()
    {
        if (playerCamera == null) return;

        float mouseX = lookInput.x;
        transform.Rotate(Vector3.up * mouseX);

        float mouseY = lookInput.y;
        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, -85.0f, 85.0f);
        playerCamera.transform.localRotation = Quaternion.Euler(cameraPitch, 0.0f, 0.0f);
    }

    private void HandleFOV()
    {
        if (playerCamera == null || Keyboard.current == null) return;

        float currentFOV = playerCamera.fieldOfView;

        if (Keyboard.current.tKey.isPressed)
        {
            currentFOV -= fovChangeSpeed * Time.deltaTime;
        }

        if (Keyboard.current.yKey.isPressed)
        {
            currentFOV += fovChangeSpeed * Time.deltaTime;
        }

        playerCamera.fieldOfView = Mathf.Clamp(currentFOV, minFOV, maxFOV);
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0.0f, maxHealth);

        if (currentHealth <= 0.0f)
        {
            Die();
        }
    }

    private void Die()
    {
        isDead = true;
        rb.linearVelocity = Vector3.zero;
        Debug.Log("El jugador ha muerto. Acciones desactivadas.");
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
        }
    }
    public void AddAmmo(int amount)
    {
        ammoCount += amount;
        Debug.Log($"Munición recogida. Balas actuales: {ammoCount}");
    }
  
}