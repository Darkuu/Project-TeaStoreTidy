using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Handles picking up, carrying, cycling, dropping, and placing items.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(FirstPersonController))]
public sealed class PlayerInventory : MonoBehaviour
{
    private InputActionAsset inputActions;
    private Transform cameraTransform;
    private FirstPersonController playerController;

    [Header("Interaction")]
    [SerializeField, Min(0.1f)] private float interactDistance = 3f;
    [SerializeField] private LayerMask interactLayers = ~0;
    [SerializeField, Min(1)] private int inventoryCapacity = 6;
    [SerializeField, Min(0f)] private float dropForwardDistance = 0.8f;
    [SerializeField, Min(0f)] private float dropDownOffset = 0.5f;

    [Header("Held Item View")]
    [SerializeField] private Vector3 heldLocalPosition =
        new Vector3(0.35f, -0.3f, 0.7f);

    [SerializeField] private Vector3 heldLocalEulerAngles;

    private int skillCapacityBonus;

    private InputAction interactAction;
    private InputAction dropAction;
    private InputAction cycleItemsAction;

    private readonly List<CarryableItem> carriedItems =
        new List<CarryableItem>(6);

    private int selectedItemIndex = -1;

    public int SelectedItemIndex => selectedItemIndex;

    public int InventoryCapacity =>
        inventoryCapacity + skillCapacityBonus;

    public int BaseInventoryCapacity => inventoryCapacity;

    public Quaternion CameraLocalRotation =>
        CameraTransform != null
            ? CameraTransform.localRotation
            : Quaternion.identity;

    public Transform CameraTransform
    {
        get
        {
            // Allows access even if another component's Awake runs first.
            if (cameraTransform == null)
            {
                if (playerController == null)
                    playerController = GetComponent<FirstPersonController>();

                if (playerController != null)
                    cameraTransform = playerController.CameraTransform;
            }

            return cameraTransform;
        }
    }

    public InputActionAsset InputActions => inputActions;

    public CarryableItem SelectedItem =>
        selectedItemIndex >= 0 &&
        selectedItemIndex < carriedItems.Count
            ? carriedItems[selectedItemIndex]
            : null;

    private void Awake()
    {
        playerController = GetComponent<FirstPersonController>();

        inputActions = playerController.InputActions;
        cameraTransform = playerController.CameraTransform;

        if (inputActions == null || cameraTransform == null)
        {
            Debug.LogError(
                "FirstPersonController needs an Input Actions asset " +
                "and a valid player Camera Transform. " +
                "PlayerInventory uses those same references.",
                this
            );

            enabled = false;
            return;
        }

        InputBindingPersistence.Apply(inputActions);

        InputActionMap playerMap =
            inputActions.FindActionMap("Player", false);

        interactAction = playerMap != null
            ? playerMap.FindAction("Interact", false)
            : null;

        dropAction = playerMap != null
            ? playerMap.FindAction("Drop", false)
            : null;

        cycleItemsAction = playerMap != null
            ? playerMap.FindAction("CycleItems", false)
            : null;

        if (interactAction == null ||
            dropAction == null ||
            cycleItemsAction == null)
        {
            Debug.LogError(
                "Input actions asset must have Player/Interact, " +
                "Player/Drop, and Player/CycleItems actions.",
                this
            );

            enabled = false;
            return;
        }

        SetupHeldItemOverlay();
    }

    private void OnEnable()
    {
        interactAction?.Enable();
        dropAction?.Enable();
        cycleItemsAction?.Enable();
    }

    private void OnDisable()
    {
        interactAction?.Disable();
        dropAction?.Disable();
        cycleItemsAction?.Disable();
    }

    private void Update()
    {
        if (playerController != null && playerController.PowersMenuOpen)
            return;

        if (carriedItems.Count == 0)
            selectedItemIndex = -1;

        CycleSelectedItem();

        if (SelectedItem != null &&
            dropAction != null &&
            dropAction.WasPressedThisFrame())
        {
            Vector3 dropPosition =
                cameraTransform.position +
                cameraTransform.forward * dropForwardDistance -
                Vector3.up * dropDownOffset;

            SelectedItem.Drop(dropPosition);
            RemoveSelectedItem();
            return;
        }

        if (interactAction == null ||
            !interactAction.WasPressedThisFrame())
        {
            return;
        }

        if (!Physics.Raycast(
                cameraTransform.position,
                cameraTransform.forward,
                out RaycastHit hit,
                interactDistance,
                interactLayers,
                QueryTriggerInteraction.Collide))
        {
            return;
        }

        if (SelectedItem != null)
        {
            PlacementSpot spot =
                hit.collider.GetComponentInParent<PlacementSpot>();

            if (spot != null && spot.TryPlace(SelectedItem))
            {
                RemoveSelectedItem();
                return;
            }
        }

        if (carriedItems.Count >= InventoryCapacity)
            return;

        CarryableItem item =
            hit.collider.GetComponentInParent<CarryableItem>();

        if (item == null || carriedItems.Contains(item))
            return;

        carriedItems.Add(item);
        selectedItemIndex = carriedItems.Count - 1;

        UpdateSelectedItemView();
    }

    private void CycleSelectedItem()
    {
        if (selectedItemIndex < 0 ||
            carriedItems.Count == 0 ||
            cycleItemsAction == null)
        {
            return;
        }

        float scroll = cycleItemsAction.ReadValue<Vector2>().y;

        if (scroll > 0.1f)
        {
            selectedItemIndex =
                (selectedItemIndex - 1 + carriedItems.Count) %
                carriedItems.Count;
        }
        else if (scroll < -0.1f)
        {
            selectedItemIndex =
                (selectedItemIndex + 1) % carriedItems.Count;
        }
        else
        {
            return;
        }

        UpdateSelectedItemView();
    }

    private void RemoveSelectedItem()
    {
        if (selectedItemIndex < 0 ||
            selectedItemIndex >= carriedItems.Count)
        {
            return;
        }

        carriedItems.RemoveAt(selectedItemIndex);

        selectedItemIndex = carriedItems.Count == 0
            ? -1
            : Mathf.Min(selectedItemIndex, carriedItems.Count - 1);

        UpdateSelectedItemView();
    }

    private void UpdateSelectedItemView()
    {
        for (int i = 0; i < carriedItems.Count; i++)
        {
            CarryableItem item = carriedItems[i];

            if (item == null)
                continue;

            if (i == selectedItemIndex)
            {
                item.ShowInHand(
                    cameraTransform,
                    heldLocalPosition,
                    heldLocalEulerAngles
                );
            }
            else
            {
                item.Stow();
            }
        }
    }

    public List<string> GetCarriedItemIds()
    {
        var ids = new List<string>(carriedItems.Count);

        foreach (CarryableItem item in carriedItems)
        {
            if (item != null)
                ids.Add(item.PersistentId);
        }

        return ids;
    }

    public bool IsCarrying(CarryableItem item)
    {
        return carriedItems.Contains(item);
    }

    public void RestoreInventoryCapacity(int capacity)
    {
        if (capacity > 0)
            inventoryCapacity = capacity;
    }

    public void SetSkillCapacityBonus(int amount)
    {
        skillCapacityBonus = Mathf.Max(0, amount);
    }

    public void RestoreInventory(
        List<string> itemIds,
        int savedSelectedIndex,
        Dictionary<string, CarryableItem> itemsById)
    {
        carriedItems.Clear();

        if (itemIds != null && itemsById != null)
        {
            foreach (string itemId in itemIds)
            {
                if (carriedItems.Count >= InventoryCapacity)
                    break;

                if (string.IsNullOrEmpty(itemId))
                    continue;

                if (itemsById.TryGetValue(itemId, out CarryableItem item) &&
                    item != null &&
                    !carriedItems.Contains(item))
                {
                    item.gameObject.SetActive(true);
                    carriedItems.Add(item);
                }
            }
        }

        selectedItemIndex = carriedItems.Count == 0
            ? -1
            : Mathf.Clamp(
                savedSelectedIndex,
                0,
                carriedItems.Count - 1
            );

        UpdateSelectedItemView();
    }

    public void RestoreCameraLocalRotation(Quaternion localRotation)
    {
        Transform viewTransform = CameraTransform;

        if (viewTransform == null)
            return;

        if (playerController != null)
        {
            // Updates both the actual camera rotation and cameraPitch.
            playerController.RestoreCameraRotation(localRotation);
        }
        else
        {
            viewTransform.localRotation = localRotation;
        }
    }

    private void SetupHeldItemOverlay()
    {
        int overlayLayer = LayerMask.NameToLayer("HeldItemOverlay");

        if (overlayLayer < 0)
        {
            Debug.LogWarning(
                "Add a 'HeldItemOverlay' layer in Project Settings > " +
                "Tags and Layers to enable held item rendering through walls.",
                this
            );

            return;
        }

        GameObject passObject =
            new GameObject("Held Item Overlay Pass");

        passObject.transform.SetParent(transform, false);

        CustomPassVolume volume =
            passObject.AddComponent<CustomPassVolume>();

        volume.isGlobal = true;
        volume.injectionPoint =
            CustomPassInjectionPoint.BeforePostProcess;

        HeldItemOverlayPass overlayPass =
            volume.AddPassOfType<HeldItemOverlayPass>();

        overlayPass.layerMask = 1 << overlayLayer;
    }
}