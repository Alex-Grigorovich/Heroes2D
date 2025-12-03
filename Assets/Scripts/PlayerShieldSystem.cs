using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerShieldSystem : MonoBehaviour
{
    [Header("Shield Settings")]
    [SerializeField] private float staminaDrainPerSecond = 5f;
    [SerializeField] private float staminaRegenPerSecond = 8f;
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float minStaminaToRaise = 10f;

    [Header("Direction Settings")]
    [SerializeField] private float directionSmoothTime = 0.1f;
    [SerializeField] private float deadZone = 0.1f;

    [Header("Animation Hold Settings")]
    [SerializeField] private float holdLastFrameDuration = 1f;
    [SerializeField] private bool enableHoldFeature = true;

    [Header("Animation Settings")]
    [SerializeField] private float raiseAnimationDuration = 0.5f; // Длительность анимации поднятия
    [SerializeField] private bool autoDetectAnimationTime = true; // Автоматически определять длительность
    [SerializeField] private bool useTriggerForAnimation = true;
    [SerializeField] private string shieldTriggerName = "ShieldRaise";
    [SerializeField] private string shieldStateName = "Shield_Raise"; // Имя состояния для автоопределения

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerCombatSystem combatSystem;

    // Состояния
    private bool isShielding = false;
    private bool isHoldingLastFrame = false;
    private bool canShield = true;
    private bool isPlayingAnimation = false;
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

    // Для заморозки анимации
    private float animatorOriginalSpeed = 1f;
    private float holdTimer = 0f;
    private float animationTimer = 0f;
    private float actualAnimationDuration = 0.5f;

    void Start()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (combatSystem != null)
            combatSystem = GetComponent<PlayerCombatSystem>();

        currentStamina = maxStamina;
        animatorOriginalSpeed = animator.speed;

        // Автоматически определяем длительность анимации
        if (autoDetectAnimationTime)
        {
            DetectAnimationDuration();
        }
        else
        {
            actualAnimationDuration = raiseAnimationDuration;
        }
    }

    // Метод для определения длительности анимации
    void DetectAnimationDuration()
    {
        actualAnimationDuration = raiseAnimationDuration; // Значение по умолчанию

        // Попробуем получить длительность из состояния анимации
        if (animator != null && !string.IsNullOrEmpty(shieldStateName))
        {
            // Получаем все состояния из контроллера
            var controller = animator.runtimeAnimatorController;
            if (controller != null)
            {
                foreach (var clip in controller.animationClips)
                {
                    if (clip.name.Contains("Shield") || clip.name.Contains("Raise"))
                    {
                        actualAnimationDuration = clip.length;
                        Debug.Log($"📏 Обнаружена анимация '{clip.name}' длительностью {clip.length:F2} сек");
                    }
                }
            }
        }

        Debug.Log($"⏱️ Фактическая длительность анимации: {actualAnimationDuration:F2} сек");
    }

    void Update()
    {
        HandleShieldInput();
        UpdateStamina();
        UpdateAnimation();

        if (isHoldingLastFrame)
        {
            UpdateHoldState();
        }
    }

    void HandleShieldInput()
    {
        // Нажатие правой кнопки мыши
        if (Input.GetMouseButtonDown(1) && canShield && currentStamina >= minStaminaToRaise)
        {
            if (!isShielding)
            {
                StartShielding();
            }
        }

        // Отпускание правой кнопки мыши
        if (Input.GetMouseButtonUp(1) && isShielding)
        {
            StopShielding();
        }
    }

    void StartShielding()
    {
        if (isShielding || isPlayingAnimation) return;

        Debug.Log($"🚀 Старт поднятия щита. Длительность анимации: {actualAnimationDuration:F2} сек");

        isShielding = true;
        isHoldingLastFrame = false;
        isPlayingAnimation = true;
        canShield = false;
        animationTimer = 0f;

        // Восстанавливаем нормальную скорость анимации
        animator.speed = animatorOriginalSpeed;

        // ЗАПУСКАЕМ АНИМАЦИЮ
        if (useTriggerForAnimation && !string.IsNullOrEmpty(shieldTriggerName))
        {
            animator.SetTrigger(shieldTriggerName);
            Debug.Log($"🎬 Активирован триггер: {shieldTriggerName}");
        }
        else
        {
            animator.SetBool("IsShielding", true);
            Debug.Log("🎬 Установлен параметр: IsShielding = true");
        }

        // Устанавливаем параметры направления
        UpdateShieldDirectionImmediate();

        // Запускаем таймер кд
        if (shieldCooldownCoroutine != null)
            StopCoroutine(shieldCooldownCoroutine);

        shieldCooldownCoroutine = StartCoroutine(ShieldCooldownRoutine());

        // Запускаем корутину для отслеживания анимации
        if (shieldAnimationCoroutine != null)
            StopCoroutine(shieldAnimationCoroutine);

        shieldAnimationCoroutine = StartCoroutine(PlayShieldAnimation());

        Debug.Log($"🛡️ Щит поднимается... (ожидание {actualAnimationDuration:F2} сек)");
    }

    IEnumerator PlayShieldAnimation()
    {
        Debug.Log($"⏳ Начало отсчета анимации: {actualAnimationDuration:F2} сек");

        // Ждем завершения анимации поднятия
        float timer = 0f;

        while (timer < actualAnimationDuration && isShielding)
        {
            timer += Time.deltaTime;
            animationTimer = timer;

            // Показываем прогресс каждые 0.1 секунды
            if (Mathf.Floor(timer * 10) != Mathf.Floor((timer - Time.deltaTime) * 10))
            {
                float progress = timer / actualAnimationDuration;
                Debug.Log($"📊 Прогресс анимации: {progress:P0} ({timer:F2}/{actualAnimationDuration:F2} сек)");
            }

            // Проверяем не отжали ли кнопку раньше времени
            if (!Input.GetMouseButton(1))
            {
                Debug.Log("🛑 Кнопка отжата во время анимации");
                StopShielding();
                yield break;
            }

            yield return null;
        }

        if (!isShielding)
        {
            Debug.Log("⚠️ Анимация прервана (щит уже опущен)");
            yield break;
        }

        isPlayingAnimation = false;
        Debug.Log($"✅ Анимация поднятия завершена! Прошло {timer:F2} сек");

        // После завершения анимации проверяем что делать дальше
        if (Input.GetMouseButton(1) && enableHoldFeature && holdLastFrameDuration > 0)
        {
            StartHoldLastFrame();
        }
        else
        {
            Debug.Log("🔽 Кнопка не зажата или функция отключена - опускаем щит");
            StopShielding();
        }
    }

    void StartHoldLastFrame()
    {
        if (!enableHoldFeature || holdLastFrameDuration <= 0)
        {
            Debug.Log("❌ Функция заморозки отключена или время = 0");
            StopShielding();
            return;
        }

        isHoldingLastFrame = true;
        holdTimer = 0f;

        // Замораживаем анимацию на последнем кадре
        animator.speed = 0f;

        Debug.Log($"❄️ Заморозка кадра на {holdLastFrameDuration} секунд");

        if (shieldHoldCoroutine != null)
            StopCoroutine(shieldHoldCoroutine);

        shieldHoldCoroutine = StartCoroutine(HoldLastFrameRoutine());
    }

    void UpdateHoldState()
    {
        holdTimer += Time.deltaTime;

        // Показываем прогресс каждые 0.2 секунды
        if (Mathf.Floor(holdTimer * 5) != Mathf.Floor((holdTimer - Time.deltaTime) * 5))
        {
            float progress = holdTimer / holdLastFrameDuration;
            Debug.Log($"⏱️ Прогресс заморозки: {progress:P0} ({holdTimer:F1}/{holdLastFrameDuration} сек)");
        }

        if (!Input.GetMouseButton(1))
        {
            Debug.Log("🛑 Кнопка отжата, прерываем заморозку");
            StopShielding();
        }

        if (GetMoveInput().magnitude > deadZone)
        {
            Debug.Log("🚶 Персонаж двигается, прерываем заморозку");
            StopShielding();
        }
    }

    IEnumerator HoldLastFrameRoutine()
    {
        Debug.Log($"⏰ Таймер заморозки запущен: {holdLastFrameDuration} сек");

        yield return new WaitForSeconds(holdLastFrameDuration);

        Debug.Log("⏰ Время заморозки истекло, опускаем щит");
        StopShielding();
    }

    void StopShielding()
    {
        if (!isShielding)
        {
            Debug.Log("ℹ️ Щит уже опущен");
            return;
        }

        Debug.Log("🔽 Начало опускания щита");

        isShielding = false;
        isHoldingLastFrame = false;
        isPlayingAnimation = false;

        // Восстанавливаем скорость анимации
        animator.speed = animatorOriginalSpeed;

        // СБРАСЫВАЕМ ПАРАМЕТРЫ АНИМАЦИИ
        if (useTriggerForAnimation)
        {
            animator.ResetTrigger(shieldTriggerName);
        }
        else
        {
            animator.SetBool("IsShielding", false);
        }

        // Останавливаем все корутины
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

        // Запускаем таймер для возможности снова поднять щит
        if (shieldCooldownCoroutine != null)
            StopCoroutine(shieldCooldownCoroutine);

        shieldCooldownCoroutine = StartCoroutine(ShieldCooldownRoutine());

        Debug.Log("🛡️ Щит опущен");
    }

    IEnumerator ShieldCooldownRoutine()
    {
        // Ждем небольшое время для предотвращения спама
        yield return new WaitForSeconds(0.1f);
        canShield = true;
        Debug.Log("🔄 Можно снова поднять щит");
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
        // Обновляем направление щита только когда щит поднят
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

    // Метод для отладки
    void OnGUI()
    {
        if (!Application.isPlaying) return;

        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.white;
        style.fontSize = 14;
        style.richText = true;

        string statusColor = isShielding ? (isHoldingLastFrame ? "yellow" : "cyan") : "white";
        string animationColor = isPlayingAnimation ? "lime" : "white";

        //GUI.Label(new Rect(10, 100, 600, 300),
        //    $"<color=cyan><b>=== SHIELD SYSTEM DEBUG ===</b></color>\n" +
        //    $"<color={statusColor}>Состояние щита: {(isShielding ? "ПОДНЯТ" : "ОПУЩЕН")}</color>\n" +
        //    $"<color={animationColor}>Анимация: {(isPlayingAnimation ? "ИГРАЕТ" : "ОСТАНОВЛЕНА")}</color>\n" +
        //    $"Заморозка: {(isHoldingLastFrame ? "АКТИВНА" : "НЕ АКТИВНА")}\n" +
        //    $"Можно поднять: {(canShield ? "ДА" : "НЕТ")}\n" +
        //    $"\n<color=yellow>Таймеры:</color>\n" +
        //    $"• Стамина: <color=green>{currentStamina:F0}</color>/{maxStamina}\n" +
        //    $"• Анимация: <color={animationColor}>{animationTimer:F2}</color>/{actualAnimationDuration:F2} сек\n" +
        //    $"• Заморозка: <color=yellow>{holdTimer:F1}</color>/{holdLastFrameDuration} сек\n" +
        //    $"• Скорость аниматора: {animator.speed:F1}\n" +
        //    $"\n<color=magenta>Настройки:</color>\n" +
        //    $"• Длит. анимации: {actualAnimationDuration:F2} сек\n" +
        //    $"• Автоопределение: {(autoDetectAnimationTime ? "ВКЛ" : "ВЫКЛ")}\n" +
        //    $"• Исп. триггер: {(useTriggerForAnimation ? "ДА" : "НЕТ")}\n" +
        //    $"• Имя триггера: {shieldTriggerName}\n" +
        //    $"• ПКМ нажата: {(Input.GetMouseButton(1) ? "ДА" : "НЕТ")}\n" +
        //    $"• Направление: X={currentShieldX:F2}, Y={currentShieldY:F2}",
        //    style);
    }

    // Методы для внешнего доступа
    public bool IsShielding() => isShielding;
    public bool IsHoldingLastFrame() => isHoldingLastFrame;
    public bool IsPlayingAnimation() => isPlayingAnimation;
    public float GetStaminaPercent() => currentStamina / maxStamina;
    public bool CanRaiseShield() => canShield && currentStamina >= minStaminaToRaise;
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

    // Метод для принудительного обновления длительности анимации
    public void UpdateAnimationDuration(float newDuration)
    {
        if (autoDetectAnimationTime)
        {
            DetectAnimationDuration();
        }
        else
        {
            actualAnimationDuration = newDuration;
            raiseAnimationDuration = newDuration;
        }
        Debug.Log($"🔄 Обновлена длительность анимации: {actualAnimationDuration:F2} сек");
    }

    public void SetHoldDuration(float duration)
    {
        holdLastFrameDuration = Mathf.Max(0, duration);
        Debug.Log($"🔄 Установлена длительность заморозки: {holdLastFrameDuration} сек");
    }

    public void SetHoldEnabled(bool enabled)
    {
        enableHoldFeature = enabled;
        Debug.Log($"🔄 Функция заморозки: {(enabled ? "ВКЛЮЧЕНА" : "ВЫКЛЮЧЕНА")}");
        if (!enabled && isHoldingLastFrame) StopShielding();
    }

    public void SetRaiseAnimationDuration(float duration)
    {
        raiseAnimationDuration = Mathf.Max(0.1f, duration);
        if (!autoDetectAnimationTime)
        {
            actualAnimationDuration = raiseAnimationDuration;
        }
        Debug.Log($"🔄 Установлена длительность анимации: {raiseAnimationDuration:F2} сек");
    }

    public void SetAutoDetect(bool autoDetect)
    {
        autoDetectAnimationTime = autoDetect;
        if (autoDetect)
        {
            DetectAnimationDuration();
        }
        else
        {
            actualAnimationDuration = raiseAnimationDuration;
        }
    }

    public void SetUseTrigger(bool useTrigger) => useTriggerForAnimation = useTrigger;
    public void SetShieldTriggerName(string name) => shieldTriggerName = name;
    public void SetShieldStateName(string name) => shieldStateName = name;

    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        Vector2 direction = new Vector2(currentShieldX, currentShieldY);
        if (direction.magnitude > 0.1f)
        {
            Gizmos.color = isHoldingLastFrame ? Color.yellow :
                          isShielding ? new Color(0, 0.5f, 1f, 0.8f) :
                          new Color(0, 1f, 1f, 0.5f);

            float lineLength = isHoldingLastFrame ? 2f : 1.5f;
            Gizmos.DrawLine(transform.position, transform.position + (Vector3)direction * lineLength);
        }
    }
}