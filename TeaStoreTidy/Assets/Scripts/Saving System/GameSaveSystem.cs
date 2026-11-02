using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable]
public sealed class SavedSceneObject
{
    public string id;
    public string parentId;

    public bool active;
    public int layer;

    public Vector3 localPosition;
    public Quaternion localRotation;
    public Vector3 localScale;

    public Vector3 worldPosition;
    public Quaternion worldRotation;

    public bool hasRigidbody;
    public bool isKinematic;
    public bool useGravity;
    public bool detectCollisions;

    public Vector3 velocity;
    public Vector3 angularVelocity;

    public bool[] behavioursEnabled;
    public bool[] collidersEnabled;
    public bool[] renderersEnabled;

    public SavedComponentState[] behaviourStates;
    public SavedComponentState[] colliderStates;
    public SavedComponentState[] rendererStates;
}

[Serializable]
public sealed class SavedComponentState
{
    public string key;
    public bool enabled;
}

[Serializable]
public sealed class GameSaveData
{
    public int version = 3;

    public List<SavedSceneObject> objects =
        new List<SavedSceneObject>();

    public List<string> carriedItemIds =
        new List<string>();

    public int selectedItemIndex = -1;

    public Vector3 playerPosition;
    public Quaternion playerRotation;
    public Quaternion cameraLocalRotation;

    public int inventoryCapacity;
    public float sprintStamina;
    public float scentCooldownRemaining;
}

/// <summary>
/// Saves scene objects, player position/rotation, camera rotation,
/// inventory, sprint state, and scene objects to persistent storage.
/// Attach to the player or one of its children.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameSaveSystem : MonoBehaviour
{
    private const float AutosaveIntervalSeconds = 300f;
    private const string SaveFileName = "TeaStoreTidySave.json";

    private static bool loadRequestedOnNextGameScene;

    private static string StaticSavePath =>
        Path.Combine(Application.persistentDataPath, SaveFileName);

    public static bool SaveExists => File.Exists(StaticSavePath);

    private readonly Dictionary<Transform, string> idsByTransform =
        new Dictionary<Transform, string>();

    private readonly Dictionary<string, Transform> transformsById =
        new Dictionary<string, Transform>();

    private Transform[] sceneTransforms;
    private Transform playerTransform;
    private PlayerInventory inventory;
    private FirstPersonController playerController;
    private SprintController sprintController;
    private ScentTrailPower scentTrailPower;

    private float nextAutosaveTime;
    private bool isLoading;

    private string SavePath => StaticSavePath;

    public static void RequestLoadOnNextGameScene(bool shouldLoad)
    {
        loadRequestedOnNextGameScene = shouldLoad;
    }

    public static void DeleteSave()
    {
        try
        {
            if (File.Exists(StaticSavePath))
                File.Delete(StaticSavePath);
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"Could not delete save file '{StaticSavePath}': " +
                exception.Message
            );
        }
    }

    private void Awake()
    {
        inventory = GetComponentInParent<PlayerInventory>();
        playerController = GetComponentInParent<FirstPersonController>();
        sprintController = GetComponentInParent<SprintController>();
        scentTrailPower = GetComponentInParent<ScentTrailPower>();
        playerTransform = playerController != null
            ? playerController.transform
            : inventory != null ? inventory.transform : transform;
        RefreshSceneObjectRegistry(true);
    }

    private void Start()
    {
        nextAutosaveTime =
            Time.unscaledTime + AutosaveIntervalSeconds;

        if (loadRequestedOnNextGameScene)
        {
            loadRequestedOnNextGameScene = false;
            StartCoroutine(LoadAfterSceneInitialization());
        }
    }

    private System.Collections.IEnumerator LoadAfterSceneInitialization()
    {
        // Let Start methods such as ShelfObjectSpawner create their runtime items first.
        yield return null;
        RefreshSceneObjectRegistry(false);
        LoadGame();
    }

    private void Update()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.f5Key.wasPressedThisFrame)
                SaveGame();

            if (Keyboard.current.f9Key.wasPressedThisFrame)
                LoadGame();
        }

        // Leave an overdue autosave pending while paused.
        if (Time.timeScale > 0f &&
            Time.unscaledTime >= nextAutosaveTime)
        {
            SaveGame();

            nextAutosaveTime =
                Time.unscaledTime + AutosaveIntervalSeconds;
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            SaveGame();
    }

    private void OnApplicationQuit()
    {
        SaveGame();
    }

    public void SaveGame()
    {
        if (isLoading)
            return;

        try
        {
            RefreshSceneObjectRegistry(false);
            var data = new GameSaveData
            {
                playerPosition = playerTransform.position,
                playerRotation = playerTransform.rotation,

                cameraLocalRotation = inventory != null
                    ? inventory.CameraLocalRotation
                    : Quaternion.identity,

                inventoryCapacity = inventory != null
                    ? inventory.BaseInventoryCapacity
                    : 0,

                carriedItemIds = inventory != null
                    ? inventory.GetCarriedItemIds()
                    : new List<string>(),

                selectedItemIndex = inventory != null
                    ? inventory.SelectedItemIndex
                    : -1,

                sprintStamina = sprintController != null
                    ? sprintController.CurrentStamina
                    : 0f,

                scentCooldownRemaining = scentTrailPower != null
                    ? scentTrailPower.CooldownRemaining
                    : 0f
            };

            foreach (Transform sceneTransform in sceneTransforms)
            {
                if (sceneTransform == null ||
                    !idsByTransform.TryGetValue(
                        sceneTransform,
                        out string id))
                {
                    continue;
                }

                Rigidbody body =
                    sceneTransform.GetComponent<Rigidbody>();

                string parentId = string.Empty;

                if (sceneTransform.parent != null &&
                    idsByTransform.TryGetValue(
                        sceneTransform.parent,
                        out string foundParentId))
                {
                    parentId = foundParentId;
                }

                var savedObject = new SavedSceneObject
                {
                    id = id,
                    parentId = parentId,

                    active = sceneTransform.gameObject.activeSelf,
                    layer = sceneTransform.gameObject.layer,

                    localPosition = sceneTransform.localPosition,
                    localRotation = sceneTransform.localRotation,
                    localScale = sceneTransform.localScale,

                    worldPosition = sceneTransform.position,
                    worldRotation = sceneTransform.rotation,

                    hasRigidbody = body != null
                };

                Behaviour[] behaviours =
                    sceneTransform.GetComponents<Behaviour>();

                savedObject.behavioursEnabled =
                    new bool[behaviours.Length];
                savedObject.behaviourStates = CaptureComponentStates(behaviours);

                for (int i = 0; i < behaviours.Length; i++)
                {
                    bool enabled = Time.timeScale <= 0f &&
                        (behaviours[i] is FirstPersonController ||
                         behaviours[i] is PlayerInventory)
                            ? true
                            : behaviours[i].enabled;
                    savedObject.behavioursEnabled[i] = enabled;
                    savedObject.behaviourStates[i].enabled = enabled;
                }

                Collider[] colliders =
                    sceneTransform.GetComponents<Collider>();

                savedObject.collidersEnabled =
                    new bool[colliders.Length];
                savedObject.colliderStates = CaptureComponentStates(colliders);

                for (int i = 0; i < colliders.Length; i++)
                {
                    savedObject.collidersEnabled[i] =
                        colliders[i].enabled;
                }

                Renderer[] renderers =
                    sceneTransform.GetComponents<Renderer>();

                savedObject.renderersEnabled =
                    new bool[renderers.Length];
                savedObject.rendererStates = CaptureComponentStates(renderers);

                for (int i = 0; i < renderers.Length; i++)
                {
                    savedObject.renderersEnabled[i] =
                        renderers[i].enabled;
                }

                if (body != null)
                {
                    savedObject.isKinematic = body.isKinematic;
                    savedObject.useGravity = body.useGravity;
                    savedObject.detectCollisions = body.detectCollisions;

                    // Kinematic bodies do not need saved velocities.
                    savedObject.velocity = body.isKinematic
                        ? Vector3.zero
                        : body.linearVelocity;

                    savedObject.angularVelocity = body.isKinematic
                        ? Vector3.zero
                        : body.angularVelocity;
                }

                data.objects.Add(savedObject);
            }

            string directory = Path.GetDirectoryName(SavePath);

            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            WriteSaveFile(JsonUtility.ToJson(data, true));
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"Could not save game to '{SavePath}': " +
                exception.Message,
                this
            );
        }
    }

    public void LoadGame()
    {
        // Avoid restoring gameplay enabled states under an open pause menu.
        if (Time.timeScale <= 0f || isLoading || !File.Exists(SavePath))
            return;

        isLoading = true;

        try
        {
            RefreshSceneObjectRegistry(false);
            GameSaveData data = JsonUtility.FromJson<GameSaveData>(
                File.ReadAllText(SavePath)
            );

            if (data == null ||
                (data.version < 1 || data.version > 3))
            {
                Debug.LogWarning(
                    "Save file is empty or uses an unsupported version.",
                    this
                );

                return;
            }

            var savedById =
                new Dictionary<string, SavedSceneObject>();

            if (data.objects != null)
            {
                foreach (SavedSceneObject savedObject in data.objects)
                {
                    if (savedObject == null ||
                        string.IsNullOrEmpty(savedObject.id))
                    {
                        continue;
                    }

                    savedById[savedObject.id] = savedObject;
                }
            }

            // Only restore objects that exist in the current scene.
            var orderedObjects = new List<SavedSceneObject>();

            foreach (SavedSceneObject savedObject in savedById.Values)
            {
                if (transformsById.TryGetValue(
                        savedObject.id,
                        out Transform sceneTransform) &&
                    sceneTransform != null)
                {
                    orderedObjects.Add(savedObject);
                }
            }

            // Detach only objects participating in this load.
            foreach (SavedSceneObject savedObject in orderedObjects)
            {
                Transform sceneTransform =
                    transformsById[savedObject.id];

                sceneTransform.SetParent(null, true);
            }

            // Restore saved parent relationships.
            foreach (SavedSceneObject savedObject in orderedObjects)
            {
                Transform sceneTransform =
                    transformsById[savedObject.id];

                Transform savedParent = null;

                if (!string.IsNullOrEmpty(savedObject.parentId))
                {
                    transformsById.TryGetValue(
                        savedObject.parentId,
                        out savedParent
                    );
                }

                if (savedParent == sceneTransform ||
                    (savedParent != null &&
                     savedParent.IsChildOf(sceneTransform)))
                {
                    Debug.LogWarning(
                        $"Invalid saved parent for '{savedObject.id}'.",
                        sceneTransform
                    );

                    savedParent = null;
                }

                sceneTransform.SetParent(savedParent, false);
            }

            // Parents must be restored before their children.
            orderedObjects.Sort((a, b) =>
            {
                Transform transformA = transformsById[a.id];
                Transform transformB = transformsById[b.id];

                return GetHierarchyDepth(transformA)
                    .CompareTo(GetHierarchyDepth(transformB));
            });

            // Restore local transforms and layers.
            foreach (SavedSceneObject savedObject in orderedObjects)
            {
                Transform sceneTransform =
                    transformsById[savedObject.id];

                sceneTransform.localPosition =
                    savedObject.localPosition;

                sceneTransform.localRotation =
                    savedObject.localRotation;

                sceneTransform.localScale =
                    savedObject.localScale;

                sceneTransform.gameObject.layer =
                    savedObject.layer;
            }

            // Restore world poses in parent-first order.
            if (data.version >= 2)
            {
                foreach (SavedSceneObject savedObject in orderedObjects)
                {
                    Transform sceneTransform =
                        transformsById[savedObject.id];

                    sceneTransform.SetPositionAndRotation(
                        savedObject.worldPosition,
                        savedObject.worldRotation
                    );
                }
            }

            // Restore physics settings after poses are applied.
            foreach (SavedSceneObject savedObject in orderedObjects)
            {
                Transform sceneTransform =
                    transformsById[savedObject.id];

                Rigidbody body =
                    sceneTransform.GetComponent<Rigidbody>();

                if (body == null || !savedObject.hasRigidbody)
                    continue;

                body.isKinematic = savedObject.isKinematic;
                body.useGravity = savedObject.useGravity;
                body.detectCollisions = savedObject.detectCollisions;

                if (!body.isKinematic)
                {
                    body.linearVelocity = savedObject.velocity;
                    body.angularVelocity = savedObject.angularVelocity;
                }
            }

            foreach (SavedSceneObject savedObject in orderedObjects)
            {
                Transform sceneTransform =
                    transformsById[savedObject.id];

                sceneTransform.gameObject.SetActive(
                    savedObject.active
                );
            }

            CarryableItem[] carryableItems =
                FindObjectsByType<CarryableItem>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                );

            var itemsById =
                new Dictionary<string, CarryableItem>();

            foreach (CarryableItem item in carryableItems)
            {
                if (item == null)
                    continue;

                item.PrepareForLoad();

                if (!string.IsNullOrEmpty(item.PersistentId))
                    itemsById[item.PersistentId] = item;
            }

            foreach (SavedSceneObject savedObject in orderedObjects)
            {
                Transform sceneTransform =
                    transformsById[savedObject.id];

                RestoreEnabledStates(
                    sceneTransform.gameObject,
                    savedObject
                );
            }

            Physics.SyncTransforms();

            PlacementProgressTracker progressTracker =
                FindAnyObjectByType<PlacementProgressTracker>();

            // Recompute skill bonuses before rebuilding inventory, since perks
            // can increase the amount the player is allowed to carry.
            if (progressTracker != null)
                progressTracker.RefreshAfterLoad();

            // Restore the player's explicit pose even without inventory.
            CharacterController characterController =
                playerTransform.GetComponent<CharacterController>();

            bool characterWasEnabled =
                characterController != null &&
                characterController.enabled;

            if (characterWasEnabled)
                characterController.enabled = false;

            try
            {
                playerTransform.SetPositionAndRotation(
                    data.playerPosition,
                    data.playerRotation
                );

                if (inventory != null)
                {
                    inventory.RestoreInventoryCapacity(
                        data.inventoryCapacity
                    );

                    // Synchronizes cameraPitch in FirstPersonController.
                    inventory.RestoreCameraLocalRotation(
                        data.cameraLocalRotation
                    );

                    inventory.RestoreInventory(
                        data.carriedItemIds,
                        data.selectedItemIndex,
                        itemsById
                    );
                }
                else if (playerController != null)
                {
                    playerController.RestoreCameraRotation(
                        data.cameraLocalRotation
                    );
                }

            }
            finally
            {
                if (characterWasEnabled && characterController != null)
                    characterController.enabled = true;
            }

            Physics.SyncTransforms();

            if (sprintController != null && data.version >= 3)
                sprintController.RestoreCurrentStamina(data.sprintStamina);

            if (scentTrailPower != null && data.version >= 3)
                scentTrailPower.RestoreCooldownRemaining(data.scentCooldownRemaining);

            nextAutosaveTime =
                Time.unscaledTime + AutosaveIntervalSeconds;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"Could not load game from '{SavePath}': " +
                exception.Message,
                this
            );
        }
        finally
        {
            isLoading = false;
        }
    }

    private static int GetHierarchyDepth(Transform target)
    {
        int depth = 0;

        while (target != null && target.parent != null)
        {
            depth++;
            target = target.parent;
        }

        return depth;
    }

    private void WriteSaveFile(string contents)
    {
        string temporaryPath = SavePath + ".tmp";
        string backupPath = SavePath + ".bak";
        File.WriteAllText(temporaryPath, contents);

        if (!File.Exists(SavePath))
        {
            File.Move(temporaryPath, SavePath);
            return;
        }

        if (File.Exists(backupPath))
            File.Delete(backupPath);

        try
        {
            File.Replace(temporaryPath, SavePath, backupPath);
        }
        catch (PlatformNotSupportedException)
        {
            File.Delete(SavePath);
            File.Move(temporaryPath, SavePath);
        }
    }

    private void RefreshSceneObjectRegistry(bool includeAllSceneObjects)
    {
        Transform[] foundTransforms = FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (Transform sceneTransform in foundTransforms)
        {
            if (sceneTransform == null || idsByTransform.ContainsKey(sceneTransform))
                continue;

            // Later-created gameplay objects are currently created by the shelf spawner.
            // Register their complete hierarchies, but don't accidentally save runtime UI/VFX.
            if (!includeAllSceneObjects &&
                sceneTransform.GetComponentInParent<CarryableItem>() == null)
                continue;

            string id = GetStableSceneId(sceneTransform);
            if (string.IsNullOrEmpty(id))
                continue;

            if (transformsById.TryGetValue(id, out Transform duplicate) && duplicate != null)
            {
                Debug.LogWarning($"Duplicate persistent scene ID '{id}'.", sceneTransform);
                continue;
            }

            idsByTransform[sceneTransform] = id;
            transformsById[id] = sceneTransform;
        }

        var registered = new List<Transform>(idsByTransform.Count);
        var destroyed = new List<Transform>();
        foreach (KeyValuePair<Transform, string> entry in idsByTransform)
        {
            if (entry.Key == null)
                destroyed.Add(entry.Key);
            else
                registered.Add(entry.Key);
        }

        foreach (Transform deadTransform in destroyed)
            idsByTransform.Remove(deadTransform);

        var staleIds = new List<string>();
        foreach (KeyValuePair<string, Transform> entry in transformsById)
            if (entry.Value == null)
                staleIds.Add(entry.Key);
        foreach (string staleId in staleIds)
            transformsById.Remove(staleId);

        sceneTransforms = registered.ToArray();
    }

    private static string GetStableSceneId(Transform target)
    {
        CarryableItem item = target.GetComponentInParent<CarryableItem>();
        if (item == null)
            return PersistentSceneId.FromTransform(target);

        if (target == item.transform)
            return item.PersistentId;

        var relativeHierarchy = new Stack<Transform>();
        for (Transform current = target; current != item.transform; current = current.parent)
            relativeHierarchy.Push(current);

        var builder = new StringBuilder(item.PersistentId);
        while (relativeHierarchy.Count > 0)
        {
            Transform current = relativeHierarchy.Pop();
            builder.Append("/child/")
                .Append(current.GetSiblingIndex())
                .Append(':')
                .Append(current.name);
        }

        return builder.ToString();
    }

    private static SavedComponentState[] CaptureComponentStates(Component[] components)
    {
        var states = new SavedComponentState[components.Length];
        for (int i = 0; i < components.Length; i++)
        {
            states[i] = new SavedComponentState
            {
                key = GetComponentKey(components, i),
                enabled = GetComponentEnabled(components[i])
            };
        }
        return states;
    }

    private static string GetComponentKey(Component[] components, int index)
    {
        Type type = components[index].GetType();
        int occurrence = 0;
        for (int i = 0; i < index; i++)
            if (components[i].GetType() == type)
                occurrence++;
        return type.AssemblyQualifiedName + "#" + occurrence;
    }

    private static bool GetComponentEnabled(Component component)
    {
        if (component is Behaviour behaviour) return behaviour.enabled;
        if (component is Collider collider) return collider.enabled;
        if (component is Renderer renderer) return renderer.enabled;
        return true;
    }

    private static void RestoreComponentStates(Component[] components, SavedComponentState[] states)
    {
        if (states == null)
            return;

        var statesByKey = new Dictionary<string, bool>();
        foreach (SavedComponentState state in states)
            if (state != null && !string.IsNullOrEmpty(state.key))
                statesByKey[state.key] = state.enabled;

        for (int i = 0; i < components.Length; i++)
        {
            if (!statesByKey.TryGetValue(GetComponentKey(components, i), out bool enabled))
                continue;

            if (components[i] is GameSaveSystem || components[i] is PauseMenuController)
                continue;
            SetComponentEnabled(components[i], enabled);
        }
    }

    private static void SetComponentEnabled(Component component, bool enabled)
    {
        if (component is Behaviour behaviour) behaviour.enabled = enabled;
        else if (component is Collider collider) collider.enabled = enabled;
        else if (component is Renderer renderer) renderer.enabled = enabled;
    }

    private static void RestoreEnabledStates(
        GameObject target,
        SavedSceneObject savedObject)
    {
        Behaviour[] behaviours = target.GetComponents<Behaviour>();

        if (savedObject.behaviourStates != null)
            RestoreComponentStates(behaviours, savedObject.behaviourStates);
        else for (int i = 0;
             savedObject.behavioursEnabled != null &&
             i < behaviours.Length &&
             i < savedObject.behavioursEnabled.Length;
             i++)
        {
            // Keep save and pause controllers in their current runtime state.
            if (behaviours[i] is GameSaveSystem ||
                behaviours[i] is PauseMenuController)
            {
                continue;
            }

            behaviours[i].enabled =
                savedObject.behavioursEnabled[i];
        }

        Collider[] colliders = target.GetComponents<Collider>();

        if (savedObject.colliderStates != null)
            RestoreComponentStates(colliders, savedObject.colliderStates);
        else for (int i = 0;
             savedObject.collidersEnabled != null &&
             i < colliders.Length &&
             i < savedObject.collidersEnabled.Length;
             i++)
        {
            colliders[i].enabled =
                savedObject.collidersEnabled[i];
        }

        Renderer[] renderers = target.GetComponents<Renderer>();

        if (savedObject.rendererStates != null)
            RestoreComponentStates(renderers, savedObject.rendererStates);
        else for (int i = 0;
             savedObject.renderersEnabled != null &&
             i < renderers.Length &&
             i < savedObject.renderersEnabled.Length;
             i++)
        {
            renderers[i].enabled =
                savedObject.renderersEnabled[i];
        }
    }
}
