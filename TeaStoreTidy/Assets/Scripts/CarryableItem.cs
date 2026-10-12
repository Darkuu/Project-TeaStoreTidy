using UnityEngine;

/// <summary>Marks an object as something the player can carry and place.</summary>
public sealed class CarryableItem : MonoBehaviour
{
    private Collider[] itemColliders;
    private bool[] colliderEnabledStates;
    private Transform[] itemTransforms;
    private int[] originalLayers;
    private Renderer[] itemRenderers;
    private bool[] rendererEnabledStates;
    private Rigidbody body;

    private void Awake()
    {
        itemColliders = GetComponentsInChildren<Collider>(true);
        itemTransforms = GetComponentsInChildren<Transform>(true);
        itemRenderers = GetComponentsInChildren<Renderer>(true);
        colliderEnabledStates = new bool[itemColliders.Length];
        for (int i = 0; i < itemColliders.Length; i++)
            colliderEnabledStates[i] = itemColliders[i].enabled;

        originalLayers = new int[itemTransforms.Length];
        for (int i = 0; i < itemTransforms.Length; i++)
            originalLayers[i] = itemTransforms[i].gameObject.layer;

        rendererEnabledStates = new bool[itemRenderers.Length];
        for (int i = 0; i < itemRenderers.Length; i++)
            rendererEnabledStates[i] = itemRenderers[i].enabled;

        body = GetComponent<Rigidbody>();
    }

    public void ShowInHand(Transform cameraTransform, Vector3 localPosition, Vector3 localEulerAngles)
    {
        transform.SetParent(cameraTransform, false);
        transform.localPosition = localPosition;
        transform.localRotation = Quaternion.Euler(localEulerAngles);
        SetRenderersVisible(true);

        int overlayLayer = LayerMask.NameToLayer("HeldItemOverlay");
        if (overlayLayer >= 0)
        {
            foreach (Transform itemTransform in itemTransforms)
                itemTransform.gameObject.layer = overlayLayer;
        }

        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }

        foreach (Collider itemCollider in itemColliders)
            itemCollider.enabled = false;
    }

    public void Stow()
    {
        SetRenderersVisible(false);
    }

    public void Place(Transform newParent, Vector3 worldPosition, Quaternion worldRotation)
    {
        transform.SetParent(newParent, true);
        transform.SetPositionAndRotation(worldPosition, worldRotation);

        RestoreColliders();
        RestoreLayers();
        SetRenderersVisible(true);

        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }
    }

    public void Drop(Vector3 worldPosition)
    {
        transform.SetParent(null, true);
        transform.position = worldPosition;

        RestoreColliders();
        RestoreLayers();
        SetRenderersVisible(true);

        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = false;
        }
    }

    private void RestoreColliders()
    {
        for (int i = 0; i < itemColliders.Length; i++)
            itemColliders[i].enabled = colliderEnabledStates[i];
    }

    private void RestoreLayers()
    {
        for (int i = 0; i < itemTransforms.Length; i++)
        {
            if (itemTransforms[i] != null)
                itemTransforms[i].gameObject.layer = originalLayers[i];
        }
    }

    private void SetRenderersVisible(bool visible)
    {
        for (int i = 0; i < itemRenderers.Length; i++)
        {
            if (itemRenderers[i] != null)
                itemRenderers[i].enabled = visible && rendererEnabledStates[i];
        }
    }
}
