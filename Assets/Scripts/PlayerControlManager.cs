using UnityEngine;

public class PlayerControlManager : MonoBehaviour
{
    private PlayerCombatSystem combatSystem;
    private PlayerShieldSystem shieldSystem;
    private HealthSystem healthSystem;

    void Start()
    {
        combatSystem = GetComponent<PlayerCombatSystem>();
        shieldSystem = GetComponent<PlayerShieldSystem>();
        healthSystem = GetComponent<HealthSystem>();
    }

    public void BlockAllControls(bool block)
    {
        // Блокируем/разблокируем движение
        if (combatSystem != null)
        {
            combatSystem.FreezeMovement(block);
        }

        // Блокируем/разблокируем щит
        if (shieldSystem != null && shieldSystem.IsShielding())
        {
            shieldSystem.StopShielding();
        }

        // Устанавливаем флаг в HealthSystem
        if (healthSystem != null)
        {
            var blockMethod = healthSystem.GetType().GetMethod("BlockMovement");
            if (blockMethod != null)
            {
                blockMethod.Invoke(healthSystem, new object[] { block });
            }
        }
    }

    public bool IsControlsBlocked()
    {
        if (healthSystem != null)
        {
            return healthSystem.IsDead() || healthSystem.IsHurting();
        }
        return false;
    }
}