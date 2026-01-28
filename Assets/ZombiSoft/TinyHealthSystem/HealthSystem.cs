//==============================================================
// HealthSystem
// HealthSystem.Instance.TakeDamage (float Damage);
// HealthSystem.Instance.HealDamage (float Heal);
// HealthSystem.Instance.UseMana (float Mana);
// HealthSystem.Instance.RestoreMana (float Mana);
// Attach to the Hero.
//==============================================================

using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using System;

public class HealthSystem : MonoBehaviour
{
    public static HealthSystem Instance;

    public Image currentHealthBar;
    public Image currentHealthGlobe;
    public Text healthText;
    public float hitPoint = 100f;
    public float maxHitPoint = 100f;

    public Image currentManaBar;
    public Image currentManaGlobe;
    public Text manaText;
    public float manaPoint = 100f;
    public float maxManaPoint = 100f;

    public event Action OnPlayerDeath; // Событие смерти

    //==============================================================
    // Animation System
    //==============================================================
    [Header("Animation System")]
    public Animator playerAnimator;

    [Header("Death Animation")]
    public string deathTrigger = "Die";
    public string deathDirectionParameter = "DeathDirection";

    [Header("Hurt Animation - 2D Blend Tree")]
    public string hurtBoolParameter = "IsHurting"; // Изменено на bool
    public string hurtHorizontalParameter = "HurtX"; // Параметр X для Blend Tree
    public string hurtVerticalParameter = "HurtY";   // Параметр Y для Blend Tree
    public float hurtAnimationDuration = 1.5f;
    public float hurtAnimationSpeed = 1f; // Регулировка скорости анимации

    [Header("Movement Control")]
    [Tooltip("Замораживать ли движение во время анимации урона")]
    public bool freezeMovementDuringHurt = true;

    [Header("Animation States")]
    private bool isDead = false;
    private bool isHurting = false;
    private Coroutine hurtCoroutine;

    [Header("Animation Interrupt")]
    public bool interruptAttacksOnHurt = true;

    // Ссылка на PlayerCombatSystem для получения направления
    private PlayerCombatSystem playerCombat;
    private Camera mainCamera;

    // Компоненты для управления движением
    private Rigidbody2D rb;
    private Vector2 originalVelocity;
    private bool originalKinematicState;
    private RigidbodyConstraints2D originalConstraints;

    // Флаги для блокировки клавиш
    private bool blockWASD = false;

    //==============================================================
    // Regenerate Health & Mana
    //==============================================================
    public bool Regenerate = true;
    public float regen = 0.1f;
    private float timeleft = 0.0f;
    public float regenUpdateInterval = 1f;

    public bool GodMode;
    public float damageOnT = 10f;

    //==============================================================
    // Awake
    //==============================================================
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    //==============================================================
    // Update
    //==============================================================
    void Update()
    {
        if (Regenerate && !isDead && !isHurting)
            Regen();

        if (Input.GetKeyDown(KeyCode.T) && !isDead)
        {
            TakeDamage(damageOnT);
        }

        // Тестирование анимаций урона по клавишам 1-8
        TestHurtAnimations();

        // Отладочная информация
        if (Input.GetKeyDown(KeyCode.F1))
        {
            Debug.Log($"HealthSystem Status: isDead={isDead}, isHurting={isHurting}, blockWASD={blockWASD}");
        }
    }

    void Start()
    {
        InitializeAnimator();
        InitializePlayerCombatReference();
        InitializeMovementComponents();
        SetHurtAnimationSpeed();

        // Добавьте инициализацию переменных
        if (rb != null)
        {
            originalKinematicState = rb.isKinematic;
            originalConstraints = rb.constraints;
        }
    }

    //==============================================================
    // Initialize Animator and PlayerCombat reference
    //==============================================================
    private void InitializeAnimator()
    {
        if (playerAnimator == null)
        {
            playerAnimator = GetComponent<Animator>();
            if (playerAnimator == null)
            {
                playerAnimator = GetComponentInChildren<Animator>();
            }
            if (playerAnimator != null)
            {
                Debug.Log("Animator automatically assigned: " + playerAnimator.name);
            }
            else
            {
                Debug.LogError("Player Animator not found!");
            }
        }
    }

    private void InitializePlayerCombatReference()
    {
        playerCombat = GetComponent<PlayerCombatSystem>();
        if (playerCombat == null)
        {
            playerCombat = GetComponentInChildren<PlayerCombatSystem>();
        }

        mainCamera = Camera.main;

        if (playerCombat != null)
        {
            Debug.Log("PlayerCombatSystem found: " + playerCombat.name);
        }
        else
        {
            Debug.LogWarning("PlayerCombatSystem not found! Using fallback direction detection.");
        }
    }

    //==============================================================
    // Initialize Movement Components
    //==============================================================
    private void InitializeMovementComponents()
    {
        // Получаем Rigidbody2D
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = GetComponentInChildren<Rigidbody2D>();
        }
    }

    //==============================================================
    // Set Hurt Animation Speed
    //==============================================================
    private void SetHurtAnimationSpeed()
    {
        if (playerAnimator != null)
        {
            // Устанавливаем скорость анимации через параметр
            playerAnimator.SetFloat("HurtSpeed", hurtAnimationSpeed);
        }
    }

    //==============================================================
    // Простой метод блокировки WASD
    //==============================================================
    public bool IsMovementBlocked()
    {
        return blockWASD || isDead;
    }

    private void BlockWASD()
    {
        if (!freezeMovementDuringHurt) return;

        Debug.Log("=== BLOCKING WASD KEYS ===");
        blockWASD = true;

        // Вызываем единый менеджер управления
        PlayerControlManager controlManager = GetComponent<PlayerControlManager>();
        if (controlManager != null)
        {
            controlManager.BlockAllControls(true);
        }
        else
        {
            // Старый код для совместимости
            if (playerCombat != null)
            {
                var method = playerCombat.GetType().GetMethod("FreezeMovement");
                if (method != null)
                {
                    method.Invoke(playerCombat, new object[] { true });
                }
            }
        }
    }

    private void UnblockWASD()
    {
        if (!freezeMovementDuringHurt) return;

        Debug.Log("=== UNBLOCKING WASD KEYS ===");
        blockWASD = false;

        // Вызываем единый менеджер управления
        PlayerControlManager controlManager = GetComponent<PlayerControlManager>();
        if (controlManager != null && !isDead)
        {
            controlManager.BlockAllControls(false);
        }
        else
        {
            // Старый код для совместимости
            if (playerCombat != null && !isDead)
            {
                var method = playerCombat.GetType().GetMethod("FreezeMovement");
                if (method != null)
                {
                    method.Invoke(playerCombat, new object[] { false });
                }
            }
        }
    }

    public void BlockMovement(bool block)
    {
        if (block)
        {
            BlockWASD();
        }
        else
        {
            UnblockWASD();
        }
    }

    //==============================================================
    // Regenerate Health & Mana
    //==============================================================
    private void Regen()
    {
        timeleft -= Time.deltaTime;
        if (timeleft <= 0.0)
        {
            if (GodMode)
            {
                HealDamage(maxHitPoint);
                RestoreMana(maxManaPoint);
            }
            else
            {
                HealDamage(regen);
                RestoreMana(regen);
            }
            UpdateGraphics();
            timeleft = regenUpdateInterval;
        }
    }

    //==============================================================
    // Health Logic
    //==============================================================
    private void UpdateHealthBar()
    {
        float ratio = hitPoint / maxHitPoint;
        currentHealthBar.rectTransform.localPosition = new Vector3(currentHealthBar.rectTransform.rect.width * ratio - currentHealthBar.rectTransform.rect.width, 0, 0);
        healthText.text = hitPoint.ToString("0") + "/" + maxHitPoint.ToString("0");
    }

    private void UpdateHealthGlobe()
    {
        float ratio = hitPoint / maxHitPoint;
        currentHealthGlobe.rectTransform.localPosition = new Vector3(0, currentHealthGlobe.rectTransform.rect.height * ratio - currentHealthGlobe.rectTransform.rect.height, 0);
        healthText.text = hitPoint.ToString("0") + "/" + maxHitPoint.ToString("0");
    }

    public void TakeDamage(float Damage)
    {
        if (GodMode || isDead || isHurting)
        {
            Debug.Log($"TakeDamage blocked - GodMode: {GodMode}, isDead: {isDead}, isHurting: {isHurting}");
            return;
        }

        Debug.Log($"=== TAKING DAMAGE: {Damage} ===");
        Debug.Log($"Current HP: {hitPoint} -> {hitPoint - Damage}");

        // Прерываем атаку, если она активна
        if (interruptAttacksOnHurt)
        {
            PlayerCombatSystem combat = GetComponent<PlayerCombatSystem>();
            if (combat != null && combat.IsAttacking)
            {
                combat.EndAttack();
                Debug.Log("⚡ Attack interrupted by damage!");
            }
        }

        hitPoint -= Damage;
        if (hitPoint < 1) hitPoint = 0;
        UpdateGraphics();

        if (hitPoint > 0)
        {
            Debug.Log("Starting hurt animation...");
            StartHurtAnimation();
        }
        else
        {
            Debug.Log("Player died from this damage!");
        }

        StartCoroutine(PlayerHurts());
    }

    public void HealDamage(float Heal)
    {
        if (isDead) return;
        hitPoint += Heal;
        if (hitPoint > maxHitPoint) hitPoint = maxHitPoint;
        UpdateGraphics();
    }

    public void SetMaxHealth(float max)
    {
        maxHitPoint += (int)(maxHitPoint * max / 100);
        UpdateGraphics();
    }

    //==============================================================
    // Mana Logic
    //==============================================================
    private void UpdateManaBar()
    {
        float ratio = manaPoint / maxManaPoint;
        currentManaBar.rectTransform.localPosition = new Vector3(currentManaBar.rectTransform.rect.width * ratio - currentManaBar.rectTransform.rect.width, 0, 0);
        manaText.text = manaPoint.ToString("0") + "/" + maxManaPoint.ToString("0");
    }

    private void UpdateManaGlobe()
    {
        float ratio = manaPoint / maxManaPoint;
        currentManaGlobe.rectTransform.localPosition = new Vector3(0, currentManaGlobe.rectTransform.rect.height * ratio - currentManaGlobe.rectTransform.rect.height, 0);
        manaText.text = manaPoint.ToString("0") + "/" + maxManaPoint.ToString("0");
    }

    public void UseMana(float Mana)
    {
        if (isDead) return;
        manaPoint -= Mana;
        if (manaPoint < 1) manaPoint = 0;
        UpdateGraphics();
    }

    public void RestoreMana(float Mana)
    {
        if (isDead) return;
        manaPoint += Mana;
        if (manaPoint > maxManaPoint) manaPoint = maxManaPoint;
        UpdateGraphics();
    }

    public void SetMaxMana(float max)
    {
        maxManaPoint += (int)(maxManaPoint * max / 100);
        UpdateGraphics();
    }

    //==============================================================
    // Update all Bars & Globes UI graphics
    //==============================================================
    private void UpdateGraphics()
    {
        UpdateHealthBar();
        UpdateHealthGlobe();
        UpdateManaBar();
        UpdateManaGlobe();
    }

    //==============================================================
    // Get current animation direction
    //==============================================================
    private Vector2 GetCurrentAnimationDirection()
    {
        Vector2 animationDirection = Vector2.zero;

        // Получаем направление непосредственно из аниматора
        if (playerAnimator != null)
        {
            float horizontal = playerAnimator.GetFloat("Horizontal");
            float vertical = playerAnimator.GetFloat("Vertical");

            animationDirection = new Vector2(horizontal, vertical);

            // Если направление почти нулевое, используем запасной вариант
            if (animationDirection.magnitude < 0.1f)
            {
                animationDirection = GetFallbackDirection();
            }
            else
            {
                animationDirection = animationDirection.normalized;
            }
        }
        else
        {
            animationDirection = GetFallbackDirection();
        }

        return animationDirection;
    }

    //==============================================================
    // Fallback direction detection
    //==============================================================
    private Vector2 GetFallbackDirection()
    {
        // Получаем направление к курсору
        if (mainCamera != null)
        {
            Vector3 mouseScreenPos = Input.mousePosition;
            mouseScreenPos.z = -mainCamera.transform.position.z;
            Vector2 mouseWorldPosition = mainCamera.ScreenToWorldPoint(mouseScreenPos);

            Vector2 direction = (mouseWorldPosition - (Vector2)transform.position).normalized;
            return direction;
        }

        // Если камера не найдена, используем последнее известное направление из аниматора
        if (playerAnimator != null)
        {
            float horizontal = playerAnimator.GetFloat("Horizontal");
            float vertical = playerAnimator.GetFloat("Vertical");
            Vector2 direction = new Vector2(horizontal, vertical).normalized;

            if (direction.magnitude > 0.1f)
            {
                return direction;
            }
        }

        // Если все остальное не сработало, используем направление вперед
        return Vector2.up;
    }

    //==============================================================
    // Hurt Animation Methods для 2D Blend Tree
    //==============================================================
    private void StartHurtAnimation()
    {
        if (playerAnimator == null || isHurting || isDead || hitPoint <= 0) return;

        // Останавливаем предыдущую анимацию урона
        if (hurtCoroutine != null)
        {
            StopCoroutine(hurtCoroutine);
        }

        // Запускаем новую анимацию урона
        hurtCoroutine = StartCoroutine(PlayHurtAnimation());
    }

    private IEnumerator PlayHurtAnimation()
    {
        isHurting = true;
        Debug.Log($"PlayHurtAnimation started. isHurting: {isHurting}");

        // Мгновенная блокировка управления
        BlockWASD();

        // Получаем текущее направление
        Vector2 direction = GetCurrentAnimationDirection();
        Debug.Log($"Hurt direction: {direction}");

        // Немедленно обновляем параметры анимации
        if (playerAnimator != null)
        {
            // Прерываем все текущие анимации
            playerAnimator.SetBool(hurtBoolParameter, false); // Сначала сбрасываем
            playerAnimator.Update(0f); // Принудительное обновление аниматора

            // Устанавливаем направление
            playerAnimator.SetFloat(hurtHorizontalParameter, direction.x);
            playerAnimator.SetFloat(hurtVerticalParameter, direction.y);

            // Включаем анимацию урона
            playerAnimator.SetBool(hurtBoolParameter, true);

            // Сбрасываем анимацию движения
            playerAnimator.SetFloat("Speed", 0f);

            Debug.Log($"Set {hurtBoolParameter} = true, HurtX: {direction.x:F2}, HurtY: {direction.y:F2}");
        }

        // Ждем завершения анимации
        float waitTime = hurtAnimationDuration / hurtAnimationSpeed;
        Debug.Log($"Waiting for {waitTime} seconds");
        yield return new WaitForSeconds(waitTime);

        // Выключаем bool параметр для выхода из состояния Hurt
        if (playerAnimator != null)
        {
            playerAnimator.SetBool(hurtBoolParameter, false);
            Debug.Log($"Set {hurtBoolParameter} = false");
        }

        // Разблокируем клавиши WASD
        UnblockWASD();

        // Сбрасываем состояние
        isHurting = false;
        hurtCoroutine = null;
        Debug.Log($"PlayHurtAnimation finished. isHurting: {isHurting}");
    }

    //==============================================================
    // Тестирование анимаций урона (8 направлений)
    //==============================================================
    private void TestHurtAnimations()
    {
        // Тестируем 8 основных направлений
        if (Input.GetKeyDown(KeyCode.Alpha1)) TestHurtDirection(new Vector2(0, -1));  // Down
        if (Input.GetKeyDown(KeyCode.Alpha2)) TestHurtDirection(new Vector2(-1, 0));  // Left
        if (Input.GetKeyDown(KeyCode.Alpha3)) TestHurtDirection(new Vector2(-0.7f, -0.7f)); // Left-Down
        if (Input.GetKeyDown(KeyCode.Alpha4)) TestHurtDirection(new Vector2(-0.7f, 0.7f));  // Left-Up
        if (Input.GetKeyDown(KeyCode.Alpha5)) TestHurtDirection(new Vector2(1, 0));   // Right
        if (Input.GetKeyDown(KeyCode.Alpha6)) TestHurtDirection(new Vector2(0.7f, -0.7f)); // Right-Down
        if (Input.GetKeyDown(KeyCode.Alpha7)) TestHurtDirection(new Vector2(0.7f, 0.7f));  // Right-Up
        if (Input.GetKeyDown(KeyCode.Alpha8)) TestHurtDirection(new Vector2(0, 1));   // Up
    }

    private void TestHurtDirection(Vector2 direction)
    {
        if (playerAnimator != null && !isDead)
        {
            // Останавливаем предыдущую корутину
            if (hurtCoroutine != null)
            {
                StopCoroutine(hurtCoroutine);
                isHurting = false;
                playerAnimator.SetBool(hurtBoolParameter, false);
                UnblockWASD(); // Разблокируем движение на всякий случай
            }

            // Устанавливаем параметры направления
            playerAnimator.SetFloat(hurtHorizontalParameter, direction.x);
            playerAnimator.SetFloat(hurtVerticalParameter, direction.y);

            // Запускаем тестовую анимацию
            hurtCoroutine = StartCoroutine(TestHurtAnimation(direction));
        }
    }

    private IEnumerator TestHurtAnimation(Vector2 direction)
    {
        isHurting = true;

        // Блокируем клавиши WASD для теста
        BlockWASD();

        // Включаем bool параметр
        playerAnimator.SetBool(hurtBoolParameter, true);

        Debug.Log($"Testing hurt animation with direction: ({direction.x:F2}, {direction.y:F2})");

        // Ждем завершения анимации
        yield return new WaitForSeconds(hurtAnimationDuration / hurtAnimationSpeed);

        // Выключаем bool параметр
        playerAnimator.SetBool(hurtBoolParameter, false);

        // Разблокируем клавиши WASD
        UnblockWASD();

        isHurting = false;
        hurtCoroutine = null;
    }

    //==============================================================
    // Play Specific Death Animation (оставляем старый метод для совместимости)
    //==============================================================
    private void PlayDeathAnimation()
    {
        if (playerAnimator == null)
        {
            Debug.LogWarning("Player Animator is not assigned!");
            return;
        }

        // Получаем текущее направление
        Vector2 direction = GetCurrentAnimationDirection();

        // Конвертируем направление в индекс (0-7) для анимации смерти
        int directionIndex = DirectionToIndex(direction);

        // Устанавливаем параметр направления
        playerAnimator.SetInteger(deathDirectionParameter, directionIndex);

        // Активируем триггер смерти
        playerAnimator.SetTrigger(deathTrigger);

        Debug.Log($"Triggering death animation with direction index: {directionIndex}");
    }

    //==============================================================
    // Convert direction vector to index для смерти (если нужно)
    //==============================================================
    private int DirectionToIndex(Vector2 direction)
    {
        if (direction.magnitude < 0.1f)
        {
            return 0; // Down как значение по умолчанию
        }

        // Нормализуем направление
        direction = direction.normalized;

        // Вычисляем угол в градусах (0° = вправо, 90° = вверх, 180° = влево, 270° = вниз)
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // Нормализуем угол от 0 до 360
        angle = (angle + 360) % 360;

        // Разделяем на 8 направлений (по 45 градусов)
        if (angle >= 337.5f || angle < 22.5f)
            return 4; // Right
        else if (angle >= 22.5f && angle < 67.5f)
            return 6; // Right-Up
        else if (angle >= 67.5f && angle < 112.5f)
            return 7; // Up
        else if (angle >= 112.5f && angle < 157.5f)
            return 3; // Left-Up
        else if (angle >= 157.5f && angle < 202.5f)
            return 1; // Left
        else if (angle >= 202.5f && angle < 247.5f)
            return 2; // Left-Down
        else if (angle >= 247.5f && angle < 292.5f)
            return 0; // Down
        else if (angle >= 292.5f && angle < 337.5f)
            return 5; // Right-Down
        else
            return 0; // Down как значение по умолчанию
    }

    //==============================================================
    // Reset Player State
    //==============================================================
    public void RespawnPlayer()
    {
        isDead = false;
        isHurting = false;
        blockWASD = false;
        hitPoint = maxHitPoint;
        manaPoint = maxManaPoint;

        // Останавливаем корутину урона
        if (hurtCoroutine != null)
        {
            StopCoroutine(hurtCoroutine);
            hurtCoroutine = null;
        }

        // Выключаем bool параметр урона в аниматоре
        if (playerAnimator != null)
        {
            playerAnimator.SetBool(hurtBoolParameter, false);
            playerAnimator.SetFloat(hurtHorizontalParameter, 0);
            playerAnimator.SetFloat(hurtVerticalParameter, 0);
            playerAnimator.ResetTrigger(deathTrigger);
        }

        // Разблокируем движение
        UnblockWASD();

        // Включаем коллайдеры
        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null)
        {
            collider.enabled = true;
        }

        // Включаем Rigidbody
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.constraints = originalConstraints;
        }

        // Включаем управление через PlayerCombatSystem
        PlayerCombatSystem combatSystem = GetComponent<PlayerCombatSystem>();
        if (combatSystem != null)
        {
            combatSystem.enabled = true; // Включаем скрипт
            combatSystem.SetDeadState(false);
        }

        if (playerAnimator != null)
        {
            playerAnimator.Rebind();
            playerAnimator.Update(0f);
        }

        UpdateGraphics();
        Debug.Log("Player respawned");
    }

    //==============================================================
    // Debug Visualization
    //==============================================================
    private void OnDrawGizmos()
    {
        // Визуализация направления в Scene View
        if (Application.isPlaying && !isDead)
        {
            Vector2 direction = GetCurrentAnimationDirection();
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, direction * 2f);
        }
    }

    //==============================================================
    // Coroutine Player Hurts
    //==============================================================
    IEnumerator PlayerHurts()
    {
        if (PopupText.Instance != null)
        {
            PopupText.Instance.Popup("Ouch!", 1f, 1f);
        }
        else
        {
            Debug.Log("Ouch! -10 HP");
        }

        // Ждем завершения анимации урона если она есть
        if (isHurting)
        {
            yield return new WaitForSeconds(hurtAnimationDuration / hurtAnimationSpeed);
        }

        // Проверяем смерть только после анимации урона
        if (hitPoint < 1)
        {
            yield return StartCoroutine(PlayerDied());
        }
    }

    //==============================================================
    // Hero is dead
    //==============================================================
    IEnumerator PlayerDied()
    {
        if (isDead) yield break;
        isDead = true;

        // Останавливаем анимацию урона если активна
        if (hurtCoroutine != null)
        {
            StopCoroutine(hurtCoroutine);
            hurtCoroutine = null;
            isHurting = false;
        }

        // Выключаем bool параметр урона в аниматоре
        if (playerAnimator != null)
        {
            playerAnimator.SetBool(hurtBoolParameter, false);
        }

        // Блокируем WASD при смерти
        blockWASD = true;

        // Вызываем событие смерти
        OnPlayerDeath?.Invoke();

        PlayDeathAnimation();

        if (PopupText.Instance != null)
        {
            PopupText.Instance.Popup("You have died!", 1f, 1f);
        }
        else
        {
            Debug.Log("You have died!");
        }

        // Отключаем управление
        DisablePlayerControls();

        yield return new WaitForSeconds(2f);
    }

    void DisablePlayerControls()
    {
        // Получаем PlayerCombatSystem и отключаем его
        PlayerCombatSystem combatSystem = GetComponent<PlayerCombatSystem>();
        if (combatSystem != null)
        {
            combatSystem.SetDeadState(true);
            combatSystem.enabled = false; // Отключаем скрипт движения
        }

        // Получаем PlayerShieldSystem и отключаем его
        PlayerShieldSystem shieldSystem = GetComponent<PlayerShieldSystem>();
        if (shieldSystem != null)
        {
            shieldSystem.enabled = false;
        }

        // Отключаем коллайдеры
        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null)
        {
            collider.enabled = false;
        }

        // Отключаем Rigidbody
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.isKinematic = true;
        }
    }

    public bool IsDead()
    {
        return isDead;
    }

    public bool IsHurting()
    {
        return isHurting;
    }



    //==============================================================
    // Public method to adjust hurt animation speed
    //==============================================================
    public void SetHurtAnimationSpeed(float speed)
    {
        hurtAnimationSpeed = Mathf.Clamp(speed, 0.5f, 3f); // Ограничиваем скорость
        SetHurtAnimationSpeed();
    }

    public void SetHurtAnimationDuration(float duration)
    {
        hurtAnimationDuration = Mathf.Clamp(duration, 0.5f, 5f); // Ограничиваем длительность
    }
}