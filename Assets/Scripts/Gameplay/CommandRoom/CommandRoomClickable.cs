using UnityEngine;

public abstract class CommandRoomClickable : MonoBehaviour
{
    [Header("Interacao")]
    public string displayName = "Controle";
    public bool canHold = true;
    [Min(0f)] public float interactionDistance = 3f;

    public virtual bool CanInteract(CommandRoomInteractor interactor, RaycastHit hit) => isActiveAndEnabled;

    public virtual void BeginInteract(CommandRoomInteractor interactor, RaycastHit hit) { }
    public virtual void HoldInteract(CommandRoomInteractor interactor, RaycastHit hit, float holdTime) { }
    public virtual void EndInteract(CommandRoomInteractor interactor) { }
}
