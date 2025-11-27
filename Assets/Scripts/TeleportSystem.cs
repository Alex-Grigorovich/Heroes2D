using UnityEngine;

public class TeleportSystem : MonoBehaviour
{
    [Header("Teleport Settings")]
    public Transform teleportExit; // Ссылка на второй спрайт (выход)
    public float teleportDelay = 0.1f; // Задержка перед телепортацией
    public bool canTeleport = true; // Можно ли телепортироваться

    [Header("Visual Effects")]
    public ParticleSystem teleportEffect;
    public AudioClip teleportSound;

    private bool isTeleporting = false;
    private GameObject player;

    void OnTriggerEnter2D(Collider2D other)
    {
        // Проверяем, что это игрок и можно телепортироваться
        if (other.CompareTag("Player") && canTeleport && !isTeleporting)
        {
            player = other.gameObject;
            StartCoroutine(TeleportPlayer());
        }
    }

    private System.Collections.IEnumerator TeleportPlayer()
    {

        if (teleportEffect != null)
            Instantiate(teleportEffect, transform.position, Quaternion.identity);

        if (teleportSound != null)
            AudioSource.PlayClipAtPoint(teleportSound, transform.position);


        isTeleporting = true;

        // Небольшая задержка перед телепортацией
        yield return new WaitForSeconds(teleportDelay);

        if (player != null && teleportExit != null)
        {
            // Телепортируем игрока к выходу
            player.transform.position = teleportExit.position;

            // Предотвращаем мгновенное возвращение
            TeleportSystem exitTeleport = teleportExit.GetComponent<TeleportSystem>();
            if (exitTeleport != null)
            {
                exitTeleport.canTeleport = false;
                StartCoroutine(EnableTeleportAfterDelay(exitTeleport, 0.5f));
            }
        }

        isTeleporting = false;


        
    }

    private System.Collections.IEnumerator EnableTeleportAfterDelay(TeleportSystem teleport, float delay)
    {
        yield return new WaitForSeconds(delay);
        teleport.canTeleport = true;
    }


}