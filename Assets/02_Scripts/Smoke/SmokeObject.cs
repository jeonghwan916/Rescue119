using FireLink119.Player;
using UnityEngine;

public class SmokeObject : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerCough playerCough = other.GetComponent<PlayerCough>();

            if (playerCough != null)
            {
                playerCough.StartCough();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerCough playerCough = other.GetComponent<PlayerCough>();

            if (playerCough != null)
            {
                playerCough.StopCough();
            }
        }
    }
}