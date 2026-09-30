using UnityEngine;

[RequireComponent(typeof(Collider))]
public class AmmoItem : MonoBehaviour
{
    [Header("Configuración del Ítem")]
    [Tooltip("Cantidad de balas que otorga este ítem al recogerlo.")]
    [SerializeField] private int ammoAmount = 5;

    private void Awake()
    {
        // Asegura que el Collider del cubo actúe como Trigger
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Verifica si el objeto que entra en contacto es el Jugador
        if (other.TryGetComponent<PlayerController>(out var player))
        {
            // Otorga las 5 balas al jugador
            player.AddAmmo(ammoAmount);
            Debug.Log($"Ítem de munición recogido. +{ammoAmount} balas.");

            // El cubo desaparece de la escena
            Destroy(gameObject);
        }
    }
}