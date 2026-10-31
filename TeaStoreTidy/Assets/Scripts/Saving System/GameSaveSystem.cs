using System;
using System.Collections.Generic;
using System.IO;
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
}

[Serializable]
public sealed class GameSaveData
{
    public int version = 2;

    public List<SavedSceneObject> objects =
        new List<SavedSceneObject>();

    public List<string> carriedItemIds =
        new List<string>();

    public int selectedItemIndex = -1;

    public Vector3 playerPosition;
    public Quaternion playerRotation;
    public Quaternion cameraLocalRotation;

    public int inventoryCapacity;
}

/// <summary>
/// Saves scene objects, player position/rotation, camera rotation,
/// and inventory to persistent storage.
/// Attach to the player GameObject.
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
    private PlayerInventory inventory;
    private FirstPersonController playerController;

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
        inventory = GetComponent<PlayerInventory>();
        playerController = GetComponent<FirstPersonController>();

        sceneTransforms = FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (Transform sceneTransform in sceneTransforms)
        {
            if (sceneTransform == null)
                continue;

            string id = PersistentSceneId.FromTransform(sceneTransform);

            if (string.IsNullOrEmpty(id))
                continue;

            if (transformsById.ContainsKey(id))
            {
                Debug.LogWarning(
                    $"Duplicate persistent scene ID '{id}'. " +
                    "Check PersistentSceneId generation.",
                    sceneTransform
                );

                continue;
            }

            idsByTransform[sceneTransform] = id;
            transformsById[id] = sceneTransform;
        }
    }

    private void Start()
    {
        nextAutosaveTime =
            Time.unscaledTime + AutosaveIntervalSeconds;

        if (loadRequestedOnNextGameScene)
        {
            loadRequestedOnNextGameScene = false;
            LoadGame();
        }
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
        // The pause-menu Save button must resume before calling this.
        if (Time.timeScale <= 0f || isLoading)
            return;

        try
        {
            var data = new GameSaveData
            {
                playerPosition = transform.position,
                playerRotation = transform.rotation,

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
                    : -1
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

                for (int i = 0; i < behaviours.Length; i++)
                {
                    savedObject.behavioursEnabled[i] =
                        behaviours[i].enabled;
                }

                Collider[] colliders =
                    sceneTransform.GetComponents<Collider>();

                savedObject.collidersEnabled =
                    new bool[colliders.Length];

                for (int i = 0; i < colliders.Length; i++)
                {
                    savedObject.collidersEnabled[i] =
                        colliders[i].enabled;
                }

                Renderer[] renderers =
                    sceneTransform.GetComponents<Renderer>();

                savedObject.renderersEnabled =
                    new bool[renderers.Length];

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

            File.WriteAllText(
                SavePath,
                JsonUtility.ToJson(data, true)
            );
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
            GameSaveData data = JsonUtility.FromJson<GameSaveData>(
                File.ReadAllText(SavePath)
            );

            if (data == null ||
                (data.version != 1 && data.version != 2))
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

            // Restore the player's explicit pose even without inventory.
            CharacterController characterController =
                GetComponent<CharacterController>();

            bool characterWasEnabled =
                characterController != null &&
                characterController.enabled;

            if (characterWasEnabled)
                characterController.enabled = false;

            try
            {
                transform.SetPositionAndRotation(
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

    private static void RestoreEnabledStates(
        GameObject target,
        SavedSceneObject savedObject)
    {
        Behaviour[] behaviours = target.GetComponents<Behaviour>();

        for (int i = 0;
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

        for (int i = 0;
             savedObject.collidersEnabled != null &&
             i < colliders.Length &&
             i < savedObject.collidersEnabled.Length;
             i++)
        {
            colliders[i].enabled =
                savedObject.collidersEnabled[i];
        }

        Renderer[] renderers = target.GetComponents<Renderer>();

        for (int i = 0;
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