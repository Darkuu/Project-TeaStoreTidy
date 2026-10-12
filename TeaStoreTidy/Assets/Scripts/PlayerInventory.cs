using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;

/// <summary>Simple one-item carry system. Interact to pick up or place.</summary>
public sealed class PlayerInventory : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("References")]
    [SerializeField] private Transform cameraTransform;

    [Header("Interaction")]
    [SerializeField, Min(0.1f)] private float interactDistance = 3f;
    [SerializeField] private LayerMask interactLayers = ~0;
    [SerializeField, Range(1, 6)] private int inventoryCapacity = 6;
    [SerializeField, Min(0f)] private float dropForwardDistance = 0.8f;
    [SerializeField, Min(0f)] private float dropDownOffset = 0.5f;

    [Header("Held Item View")]
    [SerializeField] private Vector3 heldLocalPosition = new Vector3(0.35f, -0.3f, 0.7f);
    [SerializeField] private Vector3 heldLocalEulerAngles;

    [Header("Progress HUD")]
    [SerializeField] private Canvas progressCanvas;
    [SerializeField] private Image progressPanel;
    [SerializeField] private Text progressText;

    private InputAction interactAction;
    private InputAction dropAction;
    private InputAction cycleItemsAction;
    private readonly List<CarryableItem> carriedItems = new List<CarryableItem>(6);
    private int selectedItemIndex = -1;

    private void Awake()
    {
        if (inputActions == null || cameraTransform == null)
        {
            Debug.LogError("Assign Input Actions and Camera Transform on PlayerInventory.", this);
            enabled = false;
            return;
        }

        InputActionMap playerMap = inputActions.FindActionMap("Player", false);
        interactAction = playerMap != null ? playerMap.FindAction("Interact", false) : null;
        dropAction = playerMap != null ? playerMap.FindAction("Drop", false) : null;
        cycleItemsAction = playerMap != null ? playerMap.FindAction("CycleItems", false) : null;
        if (interactAction == null || dropAction == null || cycleItemsAction == null)
        {
            Debug.LogError("Input actions asset must have Player/Interact, Player/Drop, and Player/CycleItems actions.", this);
            enabled = false;
        }

        SetupHeldItemOverlay();

        if (enabled && GetComponent<GameSaveSystem>() == null)
            gameObject.AddComponent<GameSaveSystem>();

        if (enabled)
        {
            PlacementProgressTracker tracker = GetComponent<PlacementProgressTracker>();
            if (tracker == null)
                tracker = gameObject.AddComponent<PlacementProgressTracker>();
            tracker.ConfigureUI(progressCanvas, progressPanel, progressText);
        }
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
        if (carriedItems.Count == 0)
            selectedItemIndex = -1;

        CycleSelectedItem();

        if (selectedItemIndex >= 0 && dropAction != null && dropAction.WasPressedThisFrame())
        {
            Vector3 dropPosition = cameraTransform.position +
                                   cameraTransform.forward * dropForwardDistance -
                                   Vector3.up * dropDownOffset;
            carriedItems[selectedItemIndex].Drop(dropPosition);
            RemoveSelectedItem();
            return;
        }

        if (interactAction == null || !interactAction.WasPressedThisFrame())
            return;

        if (!Physics.Raycast(cameraTransform.position, cameraTransform.forward,
                out RaycastHit hit, interactDistance, interactLayers, QueryTriggerInteraction.Collide))
            return;

        if (selectedItemIndex >= 0)
        {
            PlacementSpot spot = hit.collider.GetComponentInParent<PlacementSpot>();
            if (spot != null && spot.TryPlace(carriedItems[selectedItemIndex]))
            {
                RemoveSelectedItem();
                return;
            }
        }

        if (carriedItems.Count >= inventoryCapacity)
            return;

        CarryableItem item = hit.collider.GetComponentInParent<CarryableItem>();
        if (item == null || carriedItems.Contains(item))
            return;

        carriedItems.Add(item);
        selectedItemIndex = carriedItems.Count - 1;
        UpdateSelectedItemView();
    }

    private void CycleSelectedItem()
    {
        if (selectedItemIndex < 0 || cycleItemsAction == null)
            return;

        float scroll = cycleItemsAction.ReadValue<Vector2>().y;
        if (scroll > 0.1f)
            selectedItemIndex = (selectedItemIndex - 1 + carriedItems.Count) % carriedItems.Count;
        else if (scroll < -0.1f)
            selectedItemIndex = (selectedItemIndex + 1) % carriedItems.Count;
        else
            return;

        UpdateSelectedItemView();
    }

    private void RemoveSelectedItem()
    {
        carriedItems.RemoveAt(selectedItemIndex);
        if (carriedItems.Count == 0)
            selectedItemIndex = -1;
        else
            selectedItemIndex = Mathf.Min(selectedItemIndex, carriedItems.Count - 1);

        UpdateSelectedItemView();
    }

    private void UpdateSelectedItemView()
    {
        for (int i = 0; i < carriedItems.Count; i++)
        {
            if (i == selectedItemIndex)
                carriedItems[i].ShowInHand(cameraTransform, heldLocalPosition, heldLocalEulerAngles);
            else
                carriedItems[i].Stow();
        }
    }

    public List<string> GetCarriedItemIds()
    {
        var ids = new List<string>(carriedItems.Count);
        foreach (CarryableItem item in carriedItems)
            ids.Add(item.PersistentId);
        return ids;
    }

    public int SelectedItemIndex => selectedItemIndex;
    public Quaternion CameraLocalRotation => cameraTransform.localRotation;

    public void RestoreInventory(List<string> itemIds, int savedSelectedIndex,
        Dictionary<string, CarryableItem> itemsById)
    {
        carriedItems.Clear();

        if (itemIds != null)
        {
            foreach (string itemId in itemIds)
            {
                if (carriedItems.Count >= inventoryCapacity)
                    break;

                if (itemsById.TryGetValue(itemId, out CarryableItem item) && item != null)
                {
                    item.gameObject.SetActive(true);
                    carriedItems.Add(item);
                }
            }
        }

        selectedItemIndex = carriedItems.Count == 0
            ? -1
            : Mathf.Clamp(savedSelectedIndex, 0, carriedItems.Count - 1);
        UpdateSelectedItemView();
    }

    public void RestoreCameraLocalRotation(Quaternion localRotation)
    {
        cameraTransform.localRotation = localRotation;
        FirstPersonController controller = GetComponent<FirstPersonController>();
        if (controller != null)
            controller.RestoreCameraRotation(localRotation);
    }

    private void SetupHeldItemOverlay()
    {
        int overlayLayer = LayerMask.NameToLayer("HeldItemOverlay");
        if (overlayLayer < 0)
        {
            Debug.LogWarning("Add a 'HeldItemOverlay' layer in Project Settings > Tags and Layers to enable held item rendering through walls.", this);
            return;
        }

        GameObject passObject = new GameObject("Held Item Overlay Pass");
        passObject.transform.SetParent(transform, false);
        CustomPassVolume volume = passObject.AddComponent<CustomPassVolume>();
        volume.isGlobal = true;
        volume.injectionPoint = CustomPassInjectionPoint.BeforePostProcess;
        HeldItemOverlayPass overlayPass = volume.AddPassOfType<HeldItemOverlayPass>();
        overlayPass.layerMask = 1 << overlayLayer;
    }
}
