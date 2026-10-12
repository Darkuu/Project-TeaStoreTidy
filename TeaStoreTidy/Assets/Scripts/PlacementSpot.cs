using UnityEngine;

/// <summary>A tagged shelf or storage area with a configurable grid of item slots.</summary>
public sealed class PlacementSpot : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField, Min(1)] private int maxItems = 6;
    [SerializeField, Min(1)] private int columns = 3;
    [Tooltip("Distance between item positions along the grid origin's local X and Z axes.")]
    [SerializeField] private Vector2 spacing = new Vector2(0.5f, 0.5f);
    [Tooltip("Place this at the center of the first slot. If empty, the legacy Placement Point or this object's transform is used.")]
    [SerializeField] private Transform gridOrigin;

    [Tooltip("Legacy fallback for the grid origin. Prefer Grid Origin for new setups.")]
    [SerializeField] private Transform placementPoint;

    private string persistentId;

    public string PersistentId
    {
        get
        {
            if (string.IsNullOrEmpty(persistentId))
                persistentId = PersistentSceneId.FromTransform(transform);
            return persistentId;
        }
    }

    [Header("Grid Preview")]
    [SerializeField] private bool showGridPreview = true;
    [SerializeField, Min(0.01f)] private float previewSize = 0.2f;

    public bool Accepts(CarryableItem item)
    {
        return item != null && item.CompareTag(gameObject.tag);
    }

    public int SlotCount => Mathf.Max(1, maxItems);

    public int FilledSlotCount
    {
        get
        {
            int filled = 0;
            int columnCount = Mathf.Max(1, columns);
            CarryableItem[] placedItems = GetComponentsInChildren<CarryableItem>(true);
            Transform origin = GetGridOrigin();

            for (int slot = 0; slot < SlotCount; slot++)
            {
                Vector3 slotPosition = GetSlotLocalPosition(slot, columnCount, origin);
                if (IsSlotOccupied(slotPosition, placedItems, true))
                    filled++;
            }

            return filled;
        }
    }

    public bool IsComplete => FilledSlotCount >= SlotCount;

    public bool TryPlace(CarryableItem item)
    {
        if (!Accepts(item))
            return false;

        int slotIndex = FindFreeSlot();
        if (slotIndex < 0)
            return false;

        int columnCount = Mathf.Max(1, columns);
        int row = slotIndex / columnCount;
        int column = slotIndex % columnCount;
        Transform origin = GetGridOrigin();

        Vector3 slotOffset = new Vector3(column * spacing.x, 0f, row * spacing.y);
        Vector3 worldPosition = origin.TransformPoint(slotOffset);
        item.Place(transform, worldPosition, origin.rotation);
        return true;
    }

    private int FindFreeSlot()
    {
        int capacity = Mathf.Max(1, maxItems);
        int columnCount = Mathf.Max(1, columns);
        CarryableItem[] placedItems = GetComponentsInChildren<CarryableItem>(true);
        Transform origin = GetGridOrigin();

        for (int slot = 0; slot < capacity; slot++)
        {
            Vector3 slotPosition = GetSlotLocalPosition(slot, columnCount, origin);
            if (!IsSlotOccupied(slotPosition, placedItems, false))
                return slot;
        }

        return -1;
    }

    private Transform GetGridOrigin()
    {
        return gridOrigin != null ? gridOrigin :
            (placementPoint != null ? placementPoint : transform);
    }

    private Vector3 GetSlotLocalPosition(int slot, int columnCount, Transform origin)
    {
        int row = slot / columnCount;
        int column = slot % columnCount;
        Vector3 offset = new Vector3(column * spacing.x, 0f, row * spacing.y);
        return transform.InverseTransformPoint(origin.TransformPoint(offset));
    }

    private bool IsSlotOccupied(Vector3 slotPosition, CarryableItem[] placedItems, bool matchingTagOnly)
    {
        foreach (CarryableItem item in placedItems)
        {
            // Placement reparents items to this spot; picking them up removes them from its children.
            if (item.transform.parent != transform ||
                Vector3.SqrMagnitude(item.transform.localPosition - slotPosition) >= 0.0001f)
                continue;

            if (!matchingTagOnly || Accepts(item))
                return true;
        }

        return false;
    }

    private void OnDrawGizmos()
    {
        if (!showGridPreview)
            return;

        int capacity = Mathf.Max(1, maxItems);
        int columnCount = Mathf.Max(1, columns);
        Transform origin = GetGridOrigin();

        CarryableItem[] placedItems = GetComponentsInChildren<CarryableItem>(true);
        for (int slot = 0; slot < capacity; slot++)
        {
            Vector3 localPosition = GetSlotLocalPosition(slot, columnCount, origin);
            bool occupied = IsSlotOccupied(localPosition, placedItems, false);

            Gizmos.color = occupied ? Color.red : Color.green;
            Gizmos.DrawWireCube(transform.TransformPoint(localPosition),
                new Vector3(previewSize, previewSize, previewSize));
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin.position, origin.position + origin.right * 0.25f);
        Gizmos.DrawLine(origin.position, origin.position + origin.forward * 0.25f);
    }
}
