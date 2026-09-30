using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement; // Necesario para reiniciar la escena con F2

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movimiento")]
    [Tooltip("Velocidad de movimiento regulable desde el Inspector.")]
    [SerializeField] private float moveSpeed = 6.0f;

    [Header("Salto y Físicas de Suelo")]
    [SerializeField] private float jumpForce = 5.0f;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundDistance = 0.3f;

    [Header("Cámara y Perspectiva (1ª y 3ª Persona)")]
    [SerializeField] private Camera playerCamera;
    [Range(0.01f, 1.0f)]
    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float minFOV = 50.0f;
    [SerializeField] private float maxFOV = 120.0f;
    [SerializeField] private float fovChangeSpeed = 40.0f;

    [Tooltip("Posición local de la cámara en 1ª Persona.")]
    [SerializeField] private Vector3 firstPersonOffset = new Vector3(0f, 0.8f, 0f);
    [Tooltip("Posición local de la cámara en 3ª Persona (Vista de Hombro o Centrada).")]
    [SerializeField] private Vector3 thirdPersonOffset = new Vector3(0.5f, 1.2f, -2.5f);

    [Header("Atributos del Jugador")]
    [SerializeField] private float maxHealth = 100.0f;
    [SerializeField] private float currentHealth;
    [SerializeField] private float maxStamina = 10.0f;
    [SerializeField] private float currentStamina;
    [SerializeField] private float staminaRegenRate = 2.0f;
    [SerializeField] private float jumpStaminaCost = 5.0f;

    [Header("Ataque / Arma (Pistola)")]
    [SerializeField] private float attackRange = 20.0f;
    [SerializeField] private float attackDamage = 25.0f;
    [SerializeField] private float fireRate = 1.5f;
    [SerializeField] private int initialAmmo = 10;
    [SerializeField] private int ammoCount;
    [SerializeField] private LayerMask attackMask = ~0;

    [Header("Inventario")]
    [SerializeField] private int[] coinInventory = new int[5];
    private int currentCoinIndex = 0;

    // Componentes y estado interno
    private Rigidbody rb;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private float cameraPitch = 0.0f;
    private bool isGrounded = false;
    private bool jumpRequested = false;
    private bool isDead = false;
    private float nextTimeToFire = 0.0f;
    private bool isThirdPerson = false;
    private Vector3 initialPosition;

    public bool IsDead => isDead;
    public float CurrentHealth => currentHealth;
    public float CurrentStamina => currentStamina;
    public bool IsGrounded => isGrounded;
    public int AmmoCount => ammoCount;
    public int[] CoinInventory => coinInventory;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        initialPosition = transform.position; // Guardar posición inicial para el Respawn

        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }

        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        ResetPlayerValues();
    }

    private void Update()
    {
        // Controles de Reinicio y Reaparición globales
        HandleSystemInputs();

        if (isDead) return;

        CheckGrounded();
        RegenerateStamina();
        ReadInputs();
        HandleLook();
        HandleFOV();
        HandleCameraToggle();
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

    private void HandleSystemInputs()
    {
        if (Keyboard.current == null) return;

        // F1: Reaparecer al estar muerto con valores iniciales
        if (Keyboard.current.f1Key.wasPressedThisFrame && isDead)
        {
            Respawn();
        }

        // F2: Reiniciar la escena completa
        if (Keyboard.current.f2Key.wasPressedThisFrame)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    private void Respawn()
    {
        isDead = false;
        transform.position = initialPosition;
        rb.linearVelocity = Vector3.zero;
        ResetPlayerValues();
        Debug.Log("Jugador reaparecido en la posición inicial.");
    }

    private void ResetPlayerValues()
    {
        currentHealth = maxHealth;
        currentStamina = maxStamina;
        ammoCount = initialAmmo;
        currentCoinIndex = 0;
        coinInventory = new int[5];
    }

    private void HandleCameraToggle()
    {
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
        {
            isThirdPerson = !isThirdPerson;
            UpdateCameraOffset();
        }
    }

    private void UpdateCameraOffset()
    {
        if (playerCamera == null) return;

        // Alterna la posición local de la cámara manteniendo su jerarquía
        playerCamera.transform.localPosition = isThirdPerson ? thirdPersonOffset : firstPersonOffset;
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

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                TryShoot();
            }
        }
    }

    private void TryShoot()
    {
        if (Time.time < nextTimeToFire) return;
        if (ammoCount <= 0)
        {
            Debug.Log("¡Sin munición!");
            return;
        }

        nextTimeToFire = Time.time + (1.0f / fireRate);
        ammoCount--;

        // El Raycast lanzado desde ViewportPointToRay(0.5, 0.5) garantiza
        // que el disparo apunte exactamente al centro de la pantalla tanto en 1ª como en 3ª persona.
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, attackRange, attackMask))
        {
            Debug.Log($"Impacto en: {hit.collider.name}");

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

    private void HandleInventoryInput()
    {
        if (Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame)
        {
            int totalCoins = GetTotalCoins();
            Debug.Log($"MONEDAS TOTALES EN INVENTARIO: {totalCoins}");
        }
    }

    public void AddAmmo(int amount)
    {
        ammoCount += amount;
        Debug.Log($"Munición recogida. Balas actuales: {ammoCount}");
    }

    public void AddCoin(int coinValue)
    {
        if (currentCoinIndex < coinInventory.Length)
        {
            coinInventory[currentCoinIndex] = coinValue;
            currentCoinIndex++;
            Debug.Log($"Moneda guardada en posición [{currentCoinIndex - 1}] del array.");
        }
    }

    private int GetTotalCoins()
    {
        int total = 0;
        for (int i = 0; i < coinInventory.Length; i++)
        {
            total += coinInventory[i];
        }
        return total;
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
        Debug.Log("El jugador ha muerto. Presioná F1 para reaparecer o F2 para reiniciar escena.");
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
        }
    }
}