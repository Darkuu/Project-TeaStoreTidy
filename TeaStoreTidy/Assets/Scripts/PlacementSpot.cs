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

    [Header("Grid Preview")]
    [SerializeField] private bool showGridPreview = true;
    [SerializeField, Min(0.01f)] private float previewSize = 0.2f;

    public bool Accepts(CarryableItem item)
    {
        return item != null && item.CompareTag(gameObject.tag);
    }

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
        Transform origin = gridOrigin != null ? gridOrigin :
            (placementPoint != null ? placementPoint : transform);

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
        Transform origin = gridOrigin != null ? gridOrigin :
            (placementPoint != null ? placementPoint : transform);

        for (int slot = 0; slot < capacity; slot++)
        {
            int row = slot / columnCount;
            int column = slot % columnCount;
            Vector3 offset = new Vector3(column * spacing.x, 0f, row * spacing.y);
            Vector3 slotPosition = transform.InverseTransformPoint(origin.TransformPoint(offset));

            bool occupied = false;
            foreach (CarryableItem placedItem in placedItems)
            {
                // Placed items are parented directly to this spot. Reparenting a picked-up item
                // to the player's hold point automatically frees its slot.
                if (placedItem.transform.parent == transform &&
                    Vector3.SqrMagnitude(placedItem.transform.localPosition - slotPosition) < 0.0001f)
                {
                    occupied = true;
                    break;
                }
            }

            if (!occupied)
                return slot;
        }

        return -1;
    }

    private void OnDrawGizmos()
    {
        if (!showGridPreview)
            return;

        int capacity = Mathf.Max(1, maxItems);
        int columnCount = Mathf.Max(1, columns);
        Transform origin = gridOrigin != null ? gridOrigin :
            (placementPoint != null ? placementPoint : transform);

        CarryableItem[] placedItems = GetComponentsInChildren<CarryableItem>(true);
        for (int slot = 0; slot < capacity; slot++)
        {
            int row = slot / columnCount;
            int column = slot % columnCount;
            Vector3 offset = new Vector3(column * spacing.x, 0f, row * spacing.y);
            Vector3 localPosition = transform.InverseTransformPoint(origin.TransformPoint(offset));
            bool occupied = false;

            foreach (CarryableItem item in placedItems)
            {
                if (item.transform.parent == transform &&
                    Vector3.SqrMagnitude(item.transform.localPosition - localPosition) < 0.0001f)
                {
                    occupied = true;
                    break;
                }
            }

            Gizmos.color = occupied ? Color.red : Color.green;
            Gizmos.DrawWireCube(transform.TransformPoint(localPosition),
                new Vector3(previewSize, previewSize, previewSize));
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin.position, origin.position + origin.right * 0.25f);
        Gizmos.DrawLine(origin.position, origin.position + origin.forward * 0.25f);
    }
}
