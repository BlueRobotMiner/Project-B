using UnityEngine;

public class Interactable : MonoBehaviour
{
    [SerializeField] private string actionName = "pick up";

    public string ActionName => actionName;
}

