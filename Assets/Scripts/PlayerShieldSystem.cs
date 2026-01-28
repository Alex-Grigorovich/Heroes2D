using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Collections;

public class PlayerShieldSystem : MonoBehaviour
{
    [Header("Shield Settings")]
    [SerializeField] private float staminaDrainPerSecond = 5f;
    [SerializeField] private float staminaRegenPerSecond = 8f;
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float minStaminaToRaise = 10f;

    [Header("Movement Restrictions")]
    [SerializeField] private bool restrictMovementWhenShielding = true;
    [SerializeField] private float movementSpeedMultiplier = 0f; // 0 = полная остановка, 0.5 = 50% скорости

    [Header("Shield Collider")]
    [SerializeField] private GameObject shieldPrefab;
    [SerializeField] private Transform shieldSpawnPoint;
    [SerializeField] private float shieldOffset = 0.5f;
    [SerializeField] private float shieldScale = 1f;
    [SerializeField] private bool destroyShieldOnLower = true;

    [Header("Direction Settings")]
    [SerializeField] private float directionSmoothTime = 0.1f;
    [SerializeField] private float deadZone = 0.1f;

    [Header("Animation Hold Settings")]
    [SerializeField] private float holdLastFrameDuration = 1f;
    [SerializeField] private bool enableHoldFeature = true;

    [Header("Animation Settings")]
    [SerializeField] private float raiseAnimationDuration = 0.5f;
    [SerializeField] private bool autoDetectAnimationTime = true;
    [SerializeField] private bool useTriggerForAnimation = true;
    [SerializeField] private string shieldTriggerName = "ShieldRaise";
    [SerializeField] private string shieldStateName = "Shield_Raise";

    [Header("Attack Block Settings")]
    [SerializeField] private float attackCooldownAfterAnimation = 0.2f; // Задержка после анимации

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerCombatSystem combatSystem;
    [SerializeField] private Rigidbody2D playerRigidbody;

    // Состояния
    private bool isShielding = false;
    private bool isHoldingLastFrame = false;
    private bool canShield = true;
    private bool isPlayingAnimation = false;
    private bool isAttackCooldown = false;
    private float currentStamina;

    // Параметры для Blend Tree
    private float currentShieldX = 0f;
    private float currentShieldY = -1f;
    private float shieldVelocityX = 0f;
    private float shieldVelocityY = 0f;

    private Vector2 lastValidDirection = Vector2.down;
    private Coroutine shieldHoldCoroutine;
    private Coroutine shieldAnimationCoroutine;
    private Coroutine shieldCooldownCoroutine;
    private Coroutine attackCooldownCoroutine;

    // Ссылка на созданный щит
    private GameObject currentShieldInstance;
    private bool shieldActive = false;

    // Для заморозки анимации
    private float animatorOriginalSpeed = 1f;
    private float holdTimer = 0f;
    private float animationTimer = 0f;
    private float actualAnimationDuration = 0.5f;

    // Контроль движения
    private Vector2 lastVelocity;
    private bool wasMoving = false;

    private PlayerInput playerInput;

    void Start()
    {
        playerInput = GetComponent<PlayerInput>();

        if (animator == null)
            animator = GetComponent<Animator>();

        if (combatSystem == null)
            combatSystem = GetComponent<PlayerCombatSystem>();

        if (playerRigidbody == null)
            playerRigidbody = GetComponent<Rigidbody2D>();

        currentStamina = maxStamina;
        animatorOriginalSpeed = animator.speed;

        if (autoDetectAnimationTime)
        {
            DetectAnimationDuration();
        }
        else
        {
            actualAnimationDuration = raiseAnimationDuration;
        }

        if (shieldSpawnPoint == null)
        {
            shieldSpawnPoint = transform;
        }

        // Подписываемся на события атаки, если они существуют
        if (combatSystem != null)
        {
            // Проверяем, есть ли события через рефлексию (или создаем их в PlayerCombatSystem)
            SetupAttackEvents();
        }
    }

    void SetupAttackEvents()
    {
        // Способ 1: Если есть события
        var attackStartedEvent = combatSystem.GetType().GetEvent("OnAttackStarted");
        var attackFinishedEvent = combatSystem.GetType().GetEvent("OnAttackFinished");

        if (attackStartedEvent != null && attackFinishedEvent != null)
        {
            // Подписываемся через рефлексию
            attackStartedEvent.AddEventHandler(combatSystem, (Action)OnAttackStarted);
            attackFinishedEvent.AddEventHandler(combatSystem, (Action)OnAttackFinished);
        }
        else
        {
            // Способ 2: Если нет событий, используем публичные методы
            Debug.LogWarning("⚠️ События атаки не найдены. Используется альтернативный метод.");
            StartCoroutine(CheckAttackStateRoutine());
        }
    }

    IEnumerator CheckAttackStateRoutine()
    {
        while (true)
        {
            // Проверяем состояние атаки через публичные методы или свойства
            bool isAttacking = false;

            // Способ A: Через метод IsAttacking()
            var isAttackingMethod = combatSystem.GetType().GetMethod("IsAttacking");
            if (isAttackingMethod != null)
            {
                isAttacking = (bool)isAttackingMethod.Invoke(combatSystem, null);
            }
            // Способ B: Через свойство
            else
            {
                var isAttackingProperty = combatSystem.GetType().GetProperty("IsAttacking");
                if (isAttackingProperty != null)
                {
                    isAttacking = (bool)isAttackingProperty.GetValue(combatSystem);
                }
            }

            if (isAttacking && !isAttackCooldown)
            {
                OnAttackStarted();
            }

            yield return new WaitForSeconds(0.1f); // Проверяем каждые 0.1 секунды
        }
    }

    private void OnAttackStarted()
    {
        isAttackCooldown = true;

        // Принудительно опускаем щит, если он поднят
        if (isShielding)
        {
            StopShielding();
        }

        Debug.Log("⚔️ Атака началась - щит заблокирован");
    }

    private void OnAttackFinished()
    {
        // Запускаем кулдаун после окончания анимации
        if (attackCooldownAfterAnimation > 0)
        {
            if (attackCooldownCoroutine != null)
                StopCoroutine(attackCooldownCoroutine);

            attackCooldownCoroutine = StartCoroutine(AttackCooldownRoutine());
        }
        else
        {
            isAttackCooldown = false;
        }
    }

    IEnumerator AttackCooldownRoutine()
    {
        yield return new WaitForSeconds(attackCooldownAfterAnimation);
        isAttackCooldown = false;
        Debug.Log("✅ Кулдаун атаки окончен - можно использовать щит");
    }

    void DetectAnimationDuration()
    {
        actualAnimationDuration = raiseAnimationDuration;

        if (animator != null && !string.IsNullOrEmpty(shieldStateName))
        {
            var controller = animator.runtimeAnimatorController;
            if (controller != null)
            {
                foreach (var clip in controller.animationClips)
                {
                    if (clip.name.Contains("Shield") || clip.name.Contains("Raise"))
                    {
                        actualAnimationDuration = clip.length;
                    }
                }
            }
        }
    }

    void Update()
    {
        // Если движение заблокировано - прекращаем использование щита
        if (IsMovementBlocked() && isShielding)
        {
            StopShielding();
            return;
        }

        HandleShieldInput();
        UpdateStamina();
        UpdateAnimation();

        if (isHoldingLastFrame)
        {
            UpdateHoldState();
        }

        if (shieldActive && currentShieldInstance != null)
        {
            UpdateShieldPosition();
        }
    }

    void FixedUpdate()
    {
        // Контроль движения в FixedUpdate для физики
        ControlMovement();
    }

    void ControlMovement()
    {
        if (!restrictMovementWhenShielding || playerRigidbody == null) return;

        if (isShielding)
        {
            // Сохраняем последнюю скорость перед остановкой
            if (playerRigidbody.velocity.magnitude > 0.1f)
            {
                lastVelocity = playerRigidbody.velocity;
                wasMoving = true;
            }

            // Останавливаем или замедляем движение
            if (movementSpeedMultiplier <= 0f)
            {
                // Полная остановка
                playerRigidbody.velocity = Vector2.zero;
            }
            else
            {
                // Замедление
                playerRigidbody.velocity *= movementSpeedMultiplier;
            }
        }
        else if (wasMoving)
        {
            // Восстанавливаем движение (опционально)
            wasMoving = false;
        }
    }

    void UpdateShieldPosition()
    {
        if (currentShieldInstance == null) return;

        Vector2 direction = new Vector2(currentShieldX, currentShieldY);
        if (direction.magnitude < 0.1f)
        {
            direction = lastValidDirection;
        }

        Vector3 shieldPosition;
        if (shieldSpawnPoint != transform)
        {
            shieldPosition = shieldSpawnPoint.position;
        }
        else
        {
            shieldPosition = transform.position + (Vector3)direction.normalized * shieldOffset;
        }

        currentShieldInstance.transform.position = shieldPosition;

        if (direction.magnitude > 0.1f)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            currentShieldInstance.transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
        }
    }

    void HandleShieldInput()
    {
        // Проверяем, можно ли использовать щит
        bool canUseShieldNow = canShield &&
                              currentStamina >= minStaminaToRaise &&
                              !isAttackCooldown &&
                              !IsMovementBlocked(); // Добавляем проверку

        if (Input.GetMouseButtonDown(1) && canUseShieldNow)
        {
            if (!isShielding)
            {
                StartShielding();
            }
        }

        if (Input.GetMouseButtonUp(1) && isShielding)
        {
            StopShielding();
        }
    }

    private bool IsMovementBlocked()
    {
        return HealthSystem.Instance != null &&
               (HealthSystem.Instance.IsDead() ||
                HealthSystem.Instance.IsHurting());
    }

    void StartShielding()
    {
        if (isShielding || isPlayingAnimation) return;

        Debug.Log($"🚀 Старт поднятия щита (атака заблокирована: {isAttackCooldown})");

        isShielding = true;
        isHoldingLastFrame = false;
        isPlayingAnimation = true;
        canShield = false;
        animationTimer = 0f;

        animator.speed = animatorOriginalSpeed;

        if (useTriggerForAnimation && !string.IsNullOrEmpty(shieldTriggerName))
        {
            animator.SetTrigger(shieldTriggerName);
        }
        else
        {
            animator.SetBool("IsShielding", true);
        }

        UpdateShieldDirectionImmediate();
        ActivateShield();

        if (shieldCooldownCoroutine != null)
            StopCoroutine(shieldCooldownCoroutine);

        shieldCooldownCoroutine = StartCoroutine(ShieldCooldownRoutine());

        if (shieldAnimationCoroutine != null)
            StopCoroutine(shieldAnimationCoroutine);

        shieldAnimationCoroutine = StartCoroutine(PlayShieldAnimation());

        // Останавливаем движение при начале поднятия щита
        if (restrictMovementWhenShielding && playerRigidbody != null)
        {
            lastVelocity = playerRigidbody.velocity;
            playerRigidbody.velocity = Vector2.zero;
        }

        if (playerInput != null)
        {
            playerInput.actions["Move"].Disable();
        }
    }

    void ActivateShield()
    {
        if (shieldActive && currentShieldInstance != null) return;

        if (shieldPrefab != null)
        {
            Vector3 spawnPosition = shieldSpawnPoint != transform ?
                shieldSpawnPoint.position :
                transform.position + (Vector3)lastValidDirection.normalized * shieldOffset;

            currentShieldInstance = Instantiate(shieldPrefab, spawnPosition, Quaternion.identity);
            currentShieldInstance.transform.localScale = Vector3.one * shieldScale;

            float angle = Mathf.Atan2(lastValidDirection.y, lastValidDirection.x) * Mathf.Rad2Deg;
            currentShieldInstance.transform.rotation = Quaternion.Euler(0, 0, angle - 90f);

            SetupShieldCollider(currentShieldInstance);
            currentShieldInstance.transform.SetParent(transform, true);

            shieldActive = true;
        }
        else
        {
            Debug.LogWarning("⚠️ Префаб щита не назначен!");
        }
    }

    void SetupShieldCollider(GameObject shield)
    {
        Rigidbody2D rb = shield.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.simulated = false;
        }

        Collider2D[] colliders = shield.GetComponentsInChildren<Collider2D>();
        foreach (Collider2D collider in colliders)
        {
            collider.isTrigger = true;
        }
    }

    void DeactivateShield()
    {
        if (currentShieldInstance != null)
        {
            currentShieldInstance.transform.SetParent(null);

            if (destroyShieldOnLower)
            {
                Destroy(currentShieldInstance);
            }
            else
            {
                currentShieldInstance.SetActive(false);
            }
        }

        currentShieldInstance = null;
        shieldActive = false;
    }

    IEnumerator PlayShieldAnimation()
    {
        float timer = 0f;

        while (timer < actualAnimationDuration && isShielding)
        {
            timer += Time.deltaTime;
            animationTimer = timer;

            if (!Input.GetMouseButton(1))
            {
                StopShielding();
                yield break;
            }

            yield return null;
        }

        if (!isShielding)
        {
            yield break;
        }

        isPlayingAnimation = false;

        if (Input.GetMouseButton(1) && enableHoldFeature && holdLastFrameDuration > 0)
        {
            StartHoldLastFrame();
        }
        else
        {
            StopShielding();
        }
    }

    void StartHoldLastFrame()
    {
        if (!enableHoldFeature || holdLastFrameDuration <= 0)
        {
            StopShielding();
            return;
        }

        isHoldingLastFrame = true;
        holdTimer = 0f;
        animator.speed = 0f;

        if (shieldHoldCoroutine != null)
            StopCoroutine(shieldHoldCoroutine);

        shieldHoldCoroutine = StartCoroutine(HoldLastFrameRoutine());
    }

    void UpdateHoldState()
    {
        holdTimer += Time.deltaTime;

        if (!Input.GetMouseButton(1))
        {
            StopShielding();
        }

        if (GetMoveInput().magnitude > deadZone)
        {
            StopShielding();
        }
    }

    IEnumerator HoldLastFrameRoutine()
    {
        yield return new WaitForSeconds(holdLastFrameDuration);
        StopShielding();
    }

    public void StopShielding()
    {
        if (!isShielding) return;

        Debug.Log("🔽 Начало опускания щита");

        isShielding = false;
        isHoldingLastFrame = false;
        isPlayingAnimation = false;

        animator.speed = animatorOriginalSpeed;

        if (useTriggerForAnimation)
        {
            animator.ResetTrigger(shieldTriggerName);
        }
        else
        {
            animator.SetBool("IsShielding", false);
        }

        DeactivateShield();

        // Восстанавливаем анимацию (если нужно)
        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
        }

        if (shieldHoldCoroutine != null)
        {
            StopCoroutine(shieldHoldCoroutine);
            shieldHoldCoroutine = null;
        }

        if (shieldAnimationCoroutine != null)
        {
            StopCoroutine(shieldAnimationCoroutine);
            shieldAnimationCoroutine = null;
        }

        if (shieldCooldownCoroutine != null)
            StopCoroutine(shieldCooldownCoroutine);

        shieldCooldownCoroutine = StartCoroutine(ShieldCooldownRoutine());

        if (playerInput != null)
        {
            playerInput.actions["Move"].Enable();
        }
    }

    IEnumerator ShieldCooldownRoutine()
    {
        yield return new WaitForSeconds(0.1f);
        canShield = true;
    }

    void UpdateStamina()
    {
        if (isShielding)
        {
            currentStamina -= staminaDrainPerSecond * Time.deltaTime;
            currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina);

            if (currentStamina <= 0)
            {
                Debug.Log("💨 Стамина закончилась");
                StopShielding();
            }
        }
        else if (!isShielding)
        {
            currentStamina += staminaRegenPerSecond * Time.deltaTime;
            currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina);
        }
    }

    void UpdateAnimation()
    {
        if (isShielding && !isHoldingLastFrame)
        {
            UpdateShieldDirection();
        }
    }

    void UpdateShieldDirection()
    {
        Vector2 targetDirection = GetTargetDirection();

        float targetX = targetDirection.x;
        float targetY = targetDirection.y;

        currentShieldX = Mathf.SmoothDamp(currentShieldX, targetX, ref shieldVelocityX, directionSmoothTime);
        currentShieldY = Mathf.SmoothDamp(currentShieldY, targetY, ref shieldVelocityY, directionSmoothTime);

        animator.SetFloat("ShieldX", currentShieldX);
        animator.SetFloat("ShieldY", currentShieldY);
    }

    void UpdateShieldDirectionImmediate()
    {
        Vector2 targetDirection = GetTargetDirection();
        currentShieldX = targetDirection.x;
        currentShieldY = targetDirection.y;

        animator.SetFloat("ShieldX", currentShieldX);
        animator.SetFloat("ShieldY", currentShieldY);
    }

    Vector2 GetTargetDirection()
    {
        Vector2 direction = Vector2.zero;

        Vector2 moveInput = GetMoveInput();
        if (moveInput.magnitude > deadZone)
        {
            direction = moveInput.normalized;
            lastValidDirection = direction;
            return direction;
        }

        Vector2 mouseDirection = GetMouseDirection();
        if (mouseDirection.magnitude > deadZone)
        {
            direction = mouseDirection.normalized;
            lastValidDirection = direction;
            return direction;
        }

        return lastValidDirection;
    }

    Vector2 GetMoveInput()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        return new Vector2(horizontal, vertical);
    }

    Vector2 GetMouseDirection()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null) return lastValidDirection;

        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = -mainCamera.transform.position.z;
        Vector2 mouseWorldPos = mainCamera.ScreenToWorldPoint(mouseScreenPos);

        Vector2 direction = (mouseWorldPos - (Vector2)transform.position);

        if (direction.magnitude > 0.5f)
        {
            return direction.normalized;
        }

        return lastValidDirection;
    }

    public bool IsAttackBlocked(Vector2 attackDirection)
    {
        if (!isShielding || !shieldActive || currentShieldInstance == null)
        {
            return false;
        }

        Vector2 shieldDirection = new Vector2(currentShieldX, currentShieldY);

        if (shieldDirection.magnitude < 0.1f)
        {
            return false;
        }

        shieldDirection = shieldDirection.normalized;
        attackDirection = attackDirection.normalized;
        Vector2 attackDirectionFromPlayer = -attackDirection;

        float angle = Vector2.Angle(shieldDirection, attackDirectionFromPlayer);
        return angle < 90f;
    }

    public Vector2 GetShieldDirection()
    {
        return new Vector2(currentShieldX, currentShieldY);
    }

    public Vector3 GetShieldPosition()
    {
        if (currentShieldInstance != null)
        {
            return currentShieldInstance.transform.position;
        }
        return transform.position + (Vector3)lastValidDirection.normalized * shieldOffset;
    }

    // Методы для внешнего доступа
    public bool IsShielding() => isShielding;
    public bool IsMovementRestricted() => isShielding && restrictMovementWhenShielding;
    public bool IsShieldActive() => shieldActive && isShielding;
    public bool IsHoldingLastFrame() => isHoldingLastFrame;
    public bool IsPlayingAnimation() => isPlayingAnimation;
    public bool IsAttackCooldown() => isAttackCooldown; // Новый метод
    public float GetStaminaPercent() => currentStamina / maxStamina;
    public bool CanRaiseShield() => canShield && currentStamina >= minStaminaToRaise && !isAttackCooldown;
    public float GetHoldProgress() => holdLastFrameDuration > 0 ? Mathf.Clamp01(holdTimer / holdLastFrameDuration) : 0f;
    public float GetAnimationProgress() => actualAnimationDuration > 0 ? Mathf.Clamp01(animationTimer / actualAnimationDuration) : 0f;

    public void TakeStaminaDamage(float damage)
    {
        if (isShielding)
        {
            currentStamina -= damage;
            currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina);
            if (currentStamina <= 0) StopShielding();
        }
    }

    public void DebugShieldInfo()
    {
        Debug.Log($"=== SHIELD DEBUG INFO ===");
        Debug.Log($"isShielding: {isShielding}");
        Debug.Log($"shieldActive: {shieldActive}");
        Debug.Log($"currentShieldInstance: {currentShieldInstance != null}");
        Debug.Log($"currentStamina: {currentStamina}/{maxStamina}");
        Debug.Log($"shieldDirection: X={currentShieldX:F2}, Y={currentShieldY:F2}");
        Debug.Log($"isAttackCooldown: {isAttackCooldown}");
        Debug.Log($"=========================");
    }

    void OnDestroy()
    {
        // Отписываемся от событий при уничтожении объекта
        if (combatSystem != null)
        {
            var attackStartedEvent = combatSystem.GetType().GetEvent("OnAttackStarted");
            var attackFinishedEvent = combatSystem.GetType().GetEvent("OnAttackFinished");

            if (attackStartedEvent != null && attackFinishedEvent != null)
            {
                attackStartedEvent.RemoveEventHandler(combatSystem, (Action)OnAttackStarted);
                attackFinishedEvent.RemoveEventHandler(combatSystem, (Action)OnAttackFinished);
            }
        }
    }
}