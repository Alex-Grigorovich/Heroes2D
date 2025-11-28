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

    //==============================================================
    // Animation System
    //==============================================================
    [Header("Death Animations")]
    public Animator playerAnimator;

    [Header("Animator Parameters")]
    public string deathTrigger = "Die";
    public string deathDirectionParameter = "DeathDirection";
    private bool isDead = false;

    // Ссылка на PlayerCombatSystem для получения направления
    private PlayerCombatSystem playerCombat;
    private Camera mainCamera;

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
        if (Regenerate && !isDead)
            Regen();

        if (Input.GetKeyDown(KeyCode.T) && !isDead)
        {
            TakeDamage(damageOnT);
        }

        TestAllDirections();
    }

    void Start()
    {
        InitializeAnimator();
        InitializePlayerCombatReference();
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
        if (GodMode || isDead) return;
        hitPoint -= Damage;
        if (hitPoint < 1) hitPoint = 0;
        UpdateGraphics();
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
    // Direction Detection using PlayerCombatSystem logic
    //==============================================================
    private int GetDeathDirectionIndex()
    {
        // ТЕСТОВЫЙ РЕЖИМ - используйте клавиши 1-8 для проверки направлений
        if (Input.GetKey(KeyCode.Alpha1)) { Debug.Log("TEST: Direction 0 - Down"); return 0; }
        if (Input.GetKey(KeyCode.Alpha2)) { Debug.Log("TEST: Direction 1 - Left"); return 1; }
        if (Input.GetKey(KeyCode.Alpha3)) { Debug.Log("TEST: Direction 2 - Left-Down"); return 2; }
        if (Input.GetKey(KeyCode.Alpha4)) { Debug.Log("TEST: Direction 3 - Left-Up"); return 3; }
        if (Input.GetKey(KeyCode.Alpha5)) { Debug.Log("TEST: Direction 4 - Right"); return 4; }
        if (Input.GetKey(KeyCode.Alpha6)) { Debug.Log("TEST: Direction 5 - Right-Down"); return 5; }
        if (Input.GetKey(KeyCode.Alpha7)) { Debug.Log("TEST: Direction 6 - Right-Up"); return 6; }
        if (Input.GetKey(KeyCode.Alpha8)) { Debug.Log("TEST: Direction 7 - Up"); return 7; }

        // Используем логику определения направления из PlayerCombatSystem
        Vector2 animationDirection = GetCurrentAnimationDirection();

        Debug.Log($"Animation Direction: {animationDirection}");

        // Преобразуем направление в индекс
        int directionIndex = DirectionToIndex(animationDirection);

        Debug.Log($"Calculated Direction Index: {directionIndex}");

        return directionIndex;
    }

    //==============================================================
    // Get current animation direction using PlayerCombatSystem logic
    //==============================================================
    private Vector2 GetCurrentAnimationDirection()
    {
        Vector2 animationDirection = Vector2.zero;

        // Получаем направление непосредственно из аниматора
        // Используем те же параметры, что и в PlayerCombatSystem
        if (playerAnimator != null)
        {
            float horizontal = playerAnimator.GetFloat("Horizontal");
            float vertical = playerAnimator.GetFloat("Vertical");

            animationDirection = new Vector2(horizontal, vertical);

            Debug.Log($"From Animator - Horizontal: {horizontal}, Vertical: {vertical}");

            // Если направление почти нулевое, используем запасной вариант
            if (animationDirection.magnitude < 0.1f)
            {
                Debug.Log("Animator direction is small, using fallback");
                animationDirection = GetFallbackDirection();
            }
            else
            {
                animationDirection = animationDirection.normalized;
            }
        }
        else
        {
            Debug.Log("Animator not found, using fallback");
            animationDirection = GetFallbackDirection();
        }

        return animationDirection;
    }

    //==============================================================
    // Fallback direction detection (как в PlayerCombatSystem)
    //==============================================================
    private Vector2 GetFallbackDirection()
    {
        // Получаем направление к курсору (как в PlayerCombatSystem)
        if (mainCamera != null)
        {
            Vector3 mouseScreenPos = Input.mousePosition;
            mouseScreenPos.z = -mainCamera.transform.position.z;
            Vector2 mouseWorldPosition = mainCamera.ScreenToWorldPoint(mouseScreenPos);

            Vector2 direction = (mouseWorldPosition - (Vector2)transform.position).normalized;
            Debug.Log($"Fallback direction to mouse: {direction}");
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
                Debug.Log($"Fallback direction from animator: {direction}");
                return direction;
            }
        }

        // Если все остальное не сработало, используем направление вперед
        Debug.Log("Fallback direction: Vector2.up");
        return Vector2.up;
    }

    //==============================================================
    // Convert direction vector to index (ИСПРАВЛЕННАЯ ВЕРСИЯ)
    //==============================================================
    private int DirectionToIndex(Vector2 direction)
    {
        if (direction.magnitude < 0.1f)
        {
            Debug.Log("Direction magnitude is too small, using default (Down)");
            return 0; // Down как значение по умолчанию
        }

        // Нормализуем направление
        direction = direction.normalized;

        // Вычисляем угол в градусах (0° = вправо, 90° = вверх, 180° = влево, 270° = вниз)
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // Нормализуем угол от 0 до 360
        angle = (angle + 360) % 360;

        Debug.Log($"Direction angle: {angle}°");

        // Разделяем на 8 направлений (по 45 градусов)
        // Сопоставление должно соответствовать вашим анимациям смерти
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
    // Play Specific Death Animation
    //==============================================================
    private void PlayDeathAnimation()
    {
        if (playerAnimator == null)
        {
            Debug.LogWarning("Player Animator is not assigned!");
            return;
        }

        int directionIndex = GetDeathDirectionIndex();

        // Устанавливаем параметр направления
        playerAnimator.SetInteger(deathDirectionParameter, directionIndex);

        // Активируем триггер смерти
        playerAnimator.SetTrigger(deathTrigger);

        Debug.Log($"Triggering death animation with direction: {directionIndex}");
    }

    //==============================================================
    // TEST METHOD - Force test all directions
    //==============================================================
    private void TestAllDirections()
    {
        if (Input.GetKeyDown(KeyCode.F1)) StartCoroutine(TestDirectionSequence());
    }

    private IEnumerator TestDirectionSequence()
    {
        Debug.Log("=== STARTING DIRECTION TEST SEQUENCE ===");

        for (int i = 0; i < 8; i++)
        {
            if (playerAnimator != null)
            {
                // Сбрасываем триггер
                playerAnimator.ResetTrigger(deathTrigger);

                // Устанавливаем направление
                playerAnimator.SetInteger(deathDirectionParameter, i);

                // Активируем триггер
                playerAnimator.SetTrigger(deathTrigger);

                Debug.Log($"Testing direction {i}");
                yield return new WaitForSeconds(1.5f);
            }
        }
    }

    //==============================================================
    // Reset Player State
    //==============================================================
    public void RespawnPlayer()
    {
        isDead = false;
        hitPoint = maxHitPoint;
        manaPoint = maxManaPoint;

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

            // Подпись с координатами
#if UNITY_EDITOR
            int directionIndex = GetDeathDirectionIndex();
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2f,
                $"Dir: {directionIndex} ({direction.x:F2}, {direction.y:F2})");
#endif
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

        if (hitPoint < 1)
        {
            yield return StartCoroutine(PlayerDied());
        }
        else
            yield return null;
    }

    //==============================================================
    // Hero is dead
    //==============================================================
    IEnumerator PlayerDied()
    {
        if (isDead) yield break;
        isDead = true;

        PlayDeathAnimation();

        if (PopupText.Instance != null)
        {
            PopupText.Instance.Popup("You have died!", 1f, 1f);
        }
        else
        {
            Debug.Log("You have died!");
        }

        yield return new WaitForSeconds(2f);
    }
}