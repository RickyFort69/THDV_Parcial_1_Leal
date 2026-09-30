using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CoinItem : MonoBehaviour
{
    [Header("Configuración de la Moneda")]
    [Tooltip("Cantidad de monedas que otorga este ítem al recogerlo.")]
    [SerializeField] private int coinValue = 1;

    private void Awake()
    {
        // Asegura que el Collider de la esfera actúe como Trigger
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Verifica si el objeto que entra en contacto es el Jugador
        if (other.TryGetComponent<PlayerController>(out var player))
        {
            // Añade la moneda al inventario/contador del jugador
            player.AddCoin(coinValue);
            Debug.Log($"¡Moneda recogida! +{coinValue}");

            // La esfera desaparece de la escena
            Destroy(gameObject);
        }
    }
}