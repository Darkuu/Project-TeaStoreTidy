using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ShelfItemSpawnGroup
{
    [Tooltip("Prefab whose root tag matches the PlacementSpot tag.")]
    public CarryableItem prefab;
    [Tooltip("One or more centers for this item's random scatter areas.")]
    public Transform[] spawnPoints;
    [Tooltip("Items spawn randomly within this horizontal radius of each spawn point.")]
    [Min(0f)] public float scatterRadius = 4f;
    [Tooltip("Minimum horizontal spacing between newly spawned items and items already in the scene.")]
    [Min(0f)] public float minimumSpacing = 0.9f;
}

/// <summary>Creates any missing items needed to fill the configured placement spots.</summary>
public sealed class ShelfObjectSpawner : MonoBehaviour
{
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private bool includeInactivePlacementSpots;
    [SerializeField] private ShelfItemSpawnGroup[] itemGroups;

    private void Start()
    {
        if (spawnOnStart)
            SpawnMissingItems();
    }

    [ContextMenu("Spawn Missing Shelf Items")]
    public void SpawnMissingItems()
    {
        PlacementSpot[] spots = FindObjectsByType<PlacementSpot>(
            includeInactivePlacementSpots ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        CarryableItem[] existingItems = FindObjectsByType<CarryableItem>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        var requiredByTag = new Dictionary<string, int>();
        foreach (PlacementSpot spot in spots)
        {
            if (spot == null)
                continue;

            requiredByTag.TryGetValue(spot.tag, out int requiredCount);
            requiredByTag[spot.tag] = requiredCount + spot.SlotCount;
        }

        var existingByTag = new Dictionary<string, int>();
        var reservedPositions = new List<Vector3>(existingItems.Length);
        foreach (CarryableItem item in existingItems)
        {
            if (item == null)
                continue;

            existingByTag.TryGetValue(item.tag, out int existingCount);
            existingByTag[item.tag] = existingCount + 1;
            reservedPositions.Add(item.transform.position);
        }

        var configuredTags = new HashSet<string>();
        if (itemGroups == null)
            return;

        foreach (ShelfItemSpawnGroup group in itemGroups)
        {
            if (group == null || group.prefab == null)
                continue;

            string itemTag = group.prefab.tag;
            if (!requiredByTag.TryGetValue(itemTag, out int requiredCount))
            {
                Debug.LogWarning($"No PlacementSpot uses tag '{itemTag}', so no items were spawned for this group.", this);
                continue;
            }

            if (!configuredTags.Add(itemTag))
            {
                Debug.LogWarning($"There is more than one spawn group for tag '{itemTag}'. Only the first group is used.", this);
                continue;
            }

            int currentCount = existingByTag.TryGetValue(itemTag, out int count) ? count : 0;
            int missingCount = Mathf.Max(0, requiredCount - currentCount);
            if (missingCount == 0)
                continue;

            Transform[] spawnPoints = group.spawnPoints;
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                Debug.LogWarning($"Assign at least one spawn point for tag '{itemTag}'. Needed {missingCount} more item(s).", this);
                continue;
            }

            SpawnItems(group, missingCount, reservedPositions);
        }

        foreach (KeyValuePair<string, int> required in requiredByTag)
        {
            if (!configuredTags.Contains(required.Key))
                Debug.LogWarning($"No item prefab is configured for shelf tag '{required.Key}'.", this);
        }
    }

    private void SpawnItems(ShelfItemSpawnGroup group, int count, List<Vector3> reservedPositions)
    {
        Transform[] points = group.spawnPoints;
        int validPointCount = 0;
        foreach (Transform point in points)
            if (point != null)
                validPointCount++;

        if (validPointCount == 0)
        {
            Debug.LogWarning($"All spawn points are empty for prefab '{group.prefab.name}'.", this);
            return;
        }

        var validPoints = new List<Transform>(validPointCount);
        foreach (Transform point in points)
            if (point != null)
                validPoints.Add(point);

        for (int itemIndex = 0; itemIndex < count; itemIndex++)
        {
            int pointIndex = itemIndex % validPoints.Count;
            Transform point = validPoints[pointIndex];
            Vector3 worldPosition = FindScatterPosition(point, group, reservedPositions);
            // Random.Range for each Euler component gives spawned objects a random
            // orientation on pitch, yaw, and roll instead of only turning around up.
            Quaternion worldRotation = Quaternion.Euler(
                UnityEngine.Random.Range(0f, 360f),
                UnityEngine.Random.Range(0f, 360f),
                UnityEngine.Random.Range(0f, 360f));

            Instantiate(group.prefab, worldPosition, worldRotation);
            reservedPositions.Add(worldPosition);
        }
    }

    private static Vector3 FindScatterPosition(Transform point, ShelfItemSpawnGroup group,
        List<Vector3> reservedPositions)
    {
        Vector3 candidate = point.position;
        float minimumSpacing = Mathf.Max(0f, group.minimumSpacing);
        float minimumSpacingSquared = minimumSpacing * minimumSpacing;

        for (int attempt = 0; attempt < 40; attempt++)
        {
            Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * group.scatterRadius;
            candidate = point.TransformPoint(new Vector3(randomOffset.x, 0f, randomOffset.y));

            bool overlapsNearbyItem = false;
            foreach (Vector3 reservedPosition in reservedPositions)
            {
                Vector3 difference = candidate - reservedPosition;
                difference = Vector3.ProjectOnPlane(difference, Vector3.up);
                if (difference.sqrMagnitude < minimumSpacingSquared)
                {
                    overlapsNearbyItem = true;
                    break;
                }
            }

            if (!overlapsNearbyItem)
                return candidate;
        }

        Debug.LogWarning($"Could not find a clear scatter position for '{group.prefab.name}' after 40 tries. Increase its scatter radius or reduce minimum spacing.", point);
        return candidate;
    }

    private void OnDrawGizmosSelected()
    {
        if (itemGroups == null)
            return;

        foreach (ShelfItemSpawnGroup group in itemGroups)
        {
            if (group == null || group.spawnPoints == null)
                continue;

            Gizmos.color = Color.yellow;
            foreach (Transform point in group.spawnPoints)
            {
                if (point == null)
                    continue;

                const int segments = 32;
                Vector3 previous = point.position + point.right * group.scatterRadius;
                for (int i = 1; i <= segments; i++)
                {
                    float angle = i * Mathf.PI * 2f / segments;
                    Vector3 next = point.position +
                                   point.right * (Mathf.Cos(angle) * group.scatterRadius) +
                                   point.forward * (Mathf.Sin(angle) * group.scatterRadius);
                    Gizmos.DrawLine(previous, next);
                    previous = next;
                }
            }
        }
    }
}
