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
    public List<SavedSceneObject> objects = new List<SavedSceneObject>();
    public List<string> carriedItemIds = new List<string>();
    public int selectedItemIndex = -1;
    public Vector3 playerPosition;
    public Quaternion playerRotation;
    public Quaternion cameraLocalRotation;
    public int inventoryCapacity;
    public int powerPoints;
    public int nextInventoryUpgradeCost = 1;
    public List<SavedPowerProgress> powerProgress = new List<SavedPowerProgress>();
}

/// <summary>Saves scene object state, player view, and inventory to persistent storage.</summary>
public sealed class GameSaveSystem : MonoBehaviour
{
    private const float AutosaveIntervalSeconds = 300f;
    private const string SaveFileName = "TeaStoreTidySave.json";
    private static bool loadRequestedOnNextGameScene;
    private static string StaticSavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    public static bool SaveExists => File.Exists(StaticSavePath);

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
            Debug.LogError($"Could not delete save file '{StaticSavePath}': {exception.Message}");
        }
    }

    private readonly Dictionary<Transform, string> idsByTransform = new Dictionary<Transform, string>();
    private readonly Dictionary<string, Transform> transformsById = new Dictionary<string, Transform>();
    private Transform[] sceneTransforms;
    private PlayerInventory inventory;
    private PlacementProgressTracker progressTracker;
    private float nextAutosaveTime;

    private string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    private void Awake()
    {
        inventory = GetComponent<PlayerInventory>();
        progressTracker = FindFirstObjectByType<PlacementProgressTracker>();
        sceneTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Transform sceneTransform in sceneTransforms)
        {
            if (sceneTransform == null)
                continue;

            string id = PersistentSceneId.FromTransform(sceneTransform);
            idsByTransform[sceneTransform] = id;
            transformsById[id] = sceneTransform;
        }
    }

    private void Start()
    {
        nextAutosaveTime = Time.unscaledTime + AutosaveIntervalSeconds;
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

        if (Time.unscaledTime >= nextAutosaveTime)
        {
            SaveGame();
            nextAutosaveTime = Time.unscaledTime + AutosaveIntervalSeconds;
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
        var data = new GameSaveData
        {
            playerPosition = transform.position,
            playerRotation = transform.rotation,
            cameraLocalRotation = inventory != null ? inventory.CameraLocalRotation : Quaternion.identity,
            inventoryCapacity = inventory != null ? inventory.InventoryCapacity : 0,
            powerPoints = progressTracker != null ? progressTracker.PowerPoints : 0,
            powerProgress = progressTracker != null ? progressTracker.GetSavedPowerProgress() : new List<SavedPowerProgress>(),
            carriedItemIds = inventory != null ? inventory.GetCarriedItemIds() : new List<string>(),
            selectedItemIndex = inventory != null ? inventory.SelectedItemIndex : -1
        };

        foreach (Transform sceneTransform in sceneTransforms)
        {
            if (sceneTransform == null || !idsByTransform.TryGetValue(sceneTransform, out string id))
                continue;

            Rigidbody body = sceneTransform.GetComponent<Rigidbody>();
            string parentId = sceneTransform.parent != null &&
                              idsByTransform.TryGetValue(sceneTransform.parent, out string foundParentId)
                ? foundParentId
                : string.Empty;

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

            Behaviour[] behaviours = sceneTransform.GetComponents<Behaviour>();
            savedObject.behavioursEnabled = new bool[behaviours.Length];
            for (int i = 0; i < behaviours.Length; i++)
                savedObject.behavioursEnabled[i] = behaviours[i].enabled;

            Collider[] colliders = sceneTransform.GetComponents<Collider>();
            savedObject.collidersEnabled = new bool[colliders.Length];
            for (int i = 0; i < colliders.Length; i++)
                savedObject.collidersEnabled[i] = colliders[i].enabled;

            Renderer[] renderers = sceneTransform.GetComponents<Renderer>();
            savedObject.renderersEnabled = new bool[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                savedObject.renderersEnabled[i] = renderers[i].enabled;

            if (body != null)
            {
                savedObject.isKinematic = body.isKinematic;
                savedObject.useGravity = body.useGravity;
                savedObject.detectCollisions = body.detectCollisions;
                savedObject.velocity = body.linearVelocity;
                savedObject.angularVelocity = body.angularVelocity;
            }

            data.objects.Add(savedObject);
        }

        try
        {
            string directory = Path.GetDirectoryName(SavePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
        }
        catch (Exception exception)
        {
            Debug.LogError($"Could not save game to '{SavePath}': {exception.Message}", this);
        }
    }

    public void LoadGame()
    {
        if (!File.Exists(SavePath))
            return;

        try
        {
            GameSaveData data = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(SavePath));
            if (data == null || (data.version != 1 && data.version != 2))
            {
                Debug.LogWarning("Save file is empty or uses an unsupported version.", this);
                return;
            }

            var savedById = new Dictionary<string, SavedSceneObject>();
            if (data.objects != null)
            {
                foreach (SavedSceneObject savedObject in data.objects)
                {
                    if (savedObject != null && !string.IsNullOrEmpty(savedObject.id))
                        savedById[savedObject.id] = savedObject;
                }
            }

            // Detach first so saved parent relationships can be restored regardless of order.
            foreach (Transform sceneTransform in sceneTransforms)
            {
                if (sceneTransform != null)
                    sceneTransform.SetParent(null, false);
            }

            foreach (SavedSceneObject savedObject in savedById.Values)
            {
                if (!transformsById.TryGetValue(savedObject.id, out Transform sceneTransform) || sceneTransform == null)
                    continue;

                Transform savedParent = null;
                if (!string.IsNullOrEmpty(savedObject.parentId))
                    transformsById.TryGetValue(savedObject.parentId, out savedParent);

                sceneTransform.SetParent(savedParent, false);
            }

            foreach (SavedSceneObject savedObject in savedById.Values)
            {
                if (!transformsById.TryGetValue(savedObject.id, out Transform sceneTransform) || sceneTransform == null)
                    continue;

                sceneTransform.localPosition = savedObject.localPosition;
                sceneTransform.localRotation = savedObject.localRotation;
                sceneTransform.localScale = savedObject.localScale;
                sceneTransform.gameObject.layer = savedObject.layer;

                Rigidbody body = sceneTransform.GetComponent<Rigidbody>();
                if (body != null && savedObject.hasRigidbody)
                {
                    body.isKinematic = savedObject.isKinematic;
                    body.useGravity = savedObject.useGravity;
                    body.detectCollisions = savedObject.detectCollisions;
                    body.linearVelocity = savedObject.velocity;
                    body.angularVelocity = savedObject.angularVelocity;
                }
            }

            // Reapply world poses after all parents have been restored. This also keeps
            // correct placement if a saved parent is missing from the current scene.
            if (data.version >= 2)
            {
                foreach (SavedSceneObject savedObject in savedById.Values)
                {
                    if (transformsById.TryGetValue(savedObject.id, out Transform sceneTransform) && sceneTransform != null)
                        sceneTransform.SetPositionAndRotation(savedObject.worldPosition, savedObject.worldRotation);
                }
            }

            foreach (SavedSceneObject savedObject in savedById.Values)
            {
                if (transformsById.TryGetValue(savedObject.id, out Transform sceneTransform) && sceneTransform != null)
                    sceneTransform.gameObject.SetActive(savedObject.active);
            }

            CarryableItem[] carryableItems = FindObjectsByType<CarryableItem>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var itemsById = new Dictionary<string, CarryableItem>();
            foreach (CarryableItem item in carryableItems)
            {
                item.PrepareForLoad();
                itemsById[item.PersistentId] = item;
            }

            foreach (SavedSceneObject savedObject in savedById.Values)
            {
                if (!transformsById.TryGetValue(savedObject.id, out Transform sceneTransform) || sceneTransform == null)
                    continue;

                RestoreEnabledStates(sceneTransform.gameObject, savedObject);
            }

            Physics.SyncTransforms();

            if (inventory != null)
            {
                transform.SetPositionAndRotation(data.playerPosition, data.playerRotation);
                inventory.RestoreInventoryCapacity(data.inventoryCapacity);
                inventory.RestoreCameraLocalRotation(data.cameraLocalRotation);
                inventory.RestoreInventory(data.carriedItemIds, data.selectedItemIndex, itemsById);
            }

            if (progressTracker != null)
                progressTracker.RestorePowerProgress(data.powerPoints, data.powerProgress, data.nextInventoryUpgradeCost);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Could not load game from '{SavePath}': {exception.Message}", this);
        }
    }

    private static void RestoreEnabledStates(GameObject target, SavedSceneObject savedObject)
    {
        Behaviour[] behaviours = target.GetComponents<Behaviour>();
        for (int i = 0; savedObject.behavioursEnabled != null && i < behaviours.Length && i < savedObject.behavioursEnabled.Length; i++)
        {
            if (!(behaviours[i] is GameSaveSystem))
                behaviours[i].enabled = savedObject.behavioursEnabled[i];
        }

        Collider[] colliders = target.GetComponents<Collider>();
        for (int i = 0; savedObject.collidersEnabled != null && i < colliders.Length && i < savedObject.collidersEnabled.Length; i++)
            colliders[i].enabled = savedObject.collidersEnabled[i];

        Renderer[] renderers = target.GetComponents<Renderer>();
        for (int i = 0; savedObject.renderersEnabled != null && i < renderers.Length && i < savedObject.renderersEnabled.Length; i++)
            renderers[i].enabled = savedObject.renderersEnabled[i];
    }
}
