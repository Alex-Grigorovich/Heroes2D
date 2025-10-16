using UnityEngine;

[System.Serializable]
public class PlayerStats : MonoBehaviour
{
    [Header("Base Stats")]
    public int level = 1;
    public int experience = 0;
    public int experienceToNextLevel = 100;

    [Header("Core Attributes")]
    public int strength = 10;
    public int dexterity = 10;
    public int vitality = 10;
    public int energy = 10;

    [Header("Derived Stats")]
    public int maxHealth = 100;
    public int maxMana = 50;
    public float attackRating = 100f;
    public float defense = 10f;

    [Header("Current Values")]
    public int currentHealth;
    public int currentMana;

    [Header("Resistances")]
    public float fireResistance = 0f;
    public float coldResistance = 0f;
    public float lightningResistance = 0f;
    public float poisonResistance = 0f;

    void Start()
    {
        currentHealth = maxHealth;
        currentMana = maxMana;
        CalculateDerivedStats();
    }

    public void CalculateDerivedStats()
    {
        // Расчет производных характеристик как в Diablo 2
        maxHealth = 20 + vitality * 2 + level * 2;
        maxMana = 10 + energy * 2;
        defense = dexterity / 4f;

        // Ограничиваем текущие значения
        currentHealth = Mathf.Min(currentHealth, maxHealth);
        currentMana = Mathf.Min(currentMana, maxMana);
    }

    public void AddExperience(int exp)
    {
        experience += exp;
        if (experience >= experienceToNextLevel)
        {
            LevelUp();
        }
    }

    void LevelUp()
    {
        level++;
        experience -= experienceToNextLevel;
        experienceToNextLevel = Mathf.RoundToInt(experienceToNextLevel * 1.1f);

        // Увеличиваем характеристики при уровне
        strength += 2;
        dexterity += 2;
        vitality += 2;
        energy += 1;

        CalculateDerivedStats();
        currentHealth = maxHealth; // Полное исцеление при уровне
        currentMana = maxMana;

        Debug.Log($"🎉 Level Up! Now level {level}");
    }

    // Бонусы для боевой системы
    public int GetStrengthDamageBonus()
    {
        return strength / 5; // +1 урон за 5 силы
    }

    public int GetWeaponDamageBonus()
    {
        return dexterity / 10; // +1 урон за 10 ловкости
    }

    public float GetCriticalChanceBonus()
    {
        return dexterity * 0.001f; // +0.1% шанс крита за 1 ловкость
    }

    public void TakeDamage(int damage)
    {
        // Учет защиты
        int finalDamage = Mathf.Max(1, damage - Mathf.RoundToInt(defense));
        currentHealth -= finalDamage;

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        Debug.Log("💀 Player died!");
        // Логика смерти персонажа
    }
}