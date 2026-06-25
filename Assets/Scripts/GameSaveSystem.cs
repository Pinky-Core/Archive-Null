using System;
using System.Collections.Generic;
using System.IO;
using ArchiveNull.Evidence;
using ArchiveNull.InvestigationBoard;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class GameSaveSystem : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";
    private const string SaveFileName = "autosave.json";
    private const string LastContextPref = "archive.save.last_context";
    private const string ContextOffice = "Office";
    private const string ContextMemory = "Memory";
    private static GameSaveSystem instance;

    [SerializeField] private float autosaveInterval = 12f;

    private float nextAutosaveTime;
    private bool loaded;

    [Serializable]
    private sealed class SaveData
    {
        public int version = 1;
        public string activeScene;
        public bool hasPlayerTransform;
        public float playerX;
        public float playerY;
        public float playerZ;
        public float playerYaw;
        public List<EvidenceRecord> evidence = new();
        public List<StringPair> notes = new();
        public string operatorNotes;
        public List<Vector2Record> cardPositions = new();
        public List<Vector2Record> worldPhotoPositions = new();
        public List<StringPair> evidenceZones = new();
        public List<string> connections = new();
        public List<string> unlockedConclusions = new();
        public List<IntRecord> timelineSlots = new();
    }

    [Serializable]
    private sealed class EvidenceRecord
    {
        public string id;
        public string name;
        public string description;
        public string narrativeLine;
        public string hintText;
        public EvidenceCategory category;
        public string sourceScene;
        public string photoFile;
    }

    [Serializable]
    private sealed class StringPair
    {
        public string key;
        public string value;
    }

    [Serializable]
    private sealed class Vector2Record
    {
        public string key;
        public float x;
        public float y;
    }

    [Serializable]
    private sealed class IntRecord
    {
        public string key;
        public int value;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (string.Equals(scene.name, MainMenuSceneName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (instance != null)
        {
            instance.LoadIfNeeded();
            return;
        }

        GameObject host = new("GameSaveSystem");
        instance = host.AddComponent<GameSaveSystem>();
        DontDestroyOnLoad(host);
        instance.LoadIfNeeded();
    }

    private void OnEnable()
    {
        EvidenceInventory.Instance.OnInventoryChanged += MarkAutosaveSoon;
    }

    private void OnDisable()
    {
        if (EvidenceInventory.ExistingInstance != null)
        {
            EvidenceInventory.ExistingInstance.OnInventoryChanged -= MarkAutosaveSoon;
        }
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextAutosaveTime)
        {
            Save();
            nextAutosaveTime = Time.unscaledTime + autosaveInterval;
        }
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            Save();
        }
    }

    private void OnApplicationQuit()
    {
        Save();
    }

    public static void SaveNow()
    {
        instance?.Save();
    }

    private void LoadIfNeeded()
    {
        if (loaded)
        {
            return;
        }

        loaded = true;
        Load();
        nextAutosaveTime = Time.unscaledTime + autosaveInterval;
    }

    private void MarkAutosaveSoon()
    {
        nextAutosaveTime = Mathf.Min(nextAutosaveTime, Time.unscaledTime + 0.8f);
    }

    private void Save()
    {
        if (string.Equals(SceneManager.GetActiveScene().name, MainMenuSceneName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SaveData data = BuildSaveData();
        Directory.CreateDirectory(SaveDirectory);
        File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
        PlayerPrefs.SetString(LastContextPref, ContextMemory);
        PlayerPrefs.Save();
    }

    private SaveData BuildSaveData()
    {
        SaveData data = new()
        {
            activeScene = SceneManager.GetActiveScene().name,
            operatorNotes = EvidenceInventory.Instance.GetOperatorNotes()
        };

        FirstPersonMovement movement = FindObjectOfType<FirstPersonMovement>();
        if (movement != null)
        {
            Vector3 safePosition = movement.LastSafePosition;
            Quaternion safeRotation = movement.LastSafeRotation;
            data.hasPlayerTransform = true;
            data.playerX = safePosition.x;
            data.playerY = safePosition.y;
            data.playerZ = safePosition.z;
            data.playerYaw = safeRotation.eulerAngles.y;
        }

        IReadOnlyList<EvidenceData> evidence = EvidenceInventory.Instance.GetAllEvidence();
        for (int i = 0; i < evidence.Count; i++)
        {
            EvidenceData item = evidence[i];
            if (item == null || string.IsNullOrWhiteSpace(item.evidenceId))
            {
                continue;
            }

            data.evidence.Add(new EvidenceRecord
            {
                id = item.evidenceId,
                name = item.evidenceName,
                description = item.description,
                narrativeLine = item.narrativeLine,
                hintText = item.hintText,
                category = item.category,
                sourceScene = item.sourceSceneName,
                photoFile = SaveEvidencePhoto(item)
            });
        }

        foreach (KeyValuePair<string, string> note in EvidenceInventory.Instance.GetAllNotes())
        {
            data.notes.Add(new StringPair { key = note.Key, value = note.Value });
        }

        AddVectorRecords(data.cardPositions, BoardSessionState.CardPositions);
        AddVectorRecords(data.worldPhotoPositions, BoardSessionState.WorldPhotoPositions);
        AddStringPairs(data.evidenceZones, BoardSessionState.EvidenceZones);
        data.connections.AddRange(BoardSessionState.Connections);
        data.unlockedConclusions.AddRange(BoardSessionState.UnlockedConclusions);

        foreach (KeyValuePair<string, int> slot in BoardSessionState.TimelineSlots)
        {
            data.timelineSlots.Add(new IntRecord { key = slot.Key, value = slot.Value });
        }

        return data;
    }

    private void Load()
    {
        if (!File.Exists(SavePath))
        {
            return;
        }

        SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
        if (data == null)
        {
            return;
        }

        BoardSessionState.Clear();
        RestoreBoardData(data);
        EvidenceInventory.Instance.RestoreEvidence(CreateEvidence(data.evidence));
        EvidenceInventory.Instance.RestoreNotes(ToDictionary(data.notes), data.operatorNotes);
        TryRestorePlayerTransform(data);
    }

    private static void TryRestorePlayerTransform(SaveData data)
    {
        if (data == null || !data.hasPlayerTransform || !string.Equals(data.activeScene, SceneManager.GetActiveScene().name, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        FirstPersonMovement movement = FindObjectOfType<FirstPersonMovement>();
        if (movement == null)
        {
            return;
        }

        movement.TryRestorePosition(
            new Vector3(data.playerX, data.playerY, data.playerZ),
            Quaternion.Euler(0f, data.playerYaw, 0f));
    }

    public static void MarkOfficeContext()
    {
        PlayerPrefs.SetString(LastContextPref, ContextOffice);
        PlayerPrefs.Save();
    }

    public static bool TryLoadSavedMemoryScene()
    {
        if (!string.Equals(PlayerPrefs.GetString(LastContextPref, ContextOffice), ContextMemory, StringComparison.OrdinalIgnoreCase) || !File.Exists(SavePath))
        {
            return false;
        }

        SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
        if (data == null || string.IsNullOrWhiteSpace(data.activeScene) || string.Equals(data.activeScene, MainMenuSceneName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        SceneManager.LoadScene(data.activeScene, LoadSceneMode.Single);
        return true;
    }

    public static void DeleteAllGameplayData()
    {
        if (Directory.Exists(SaveDirectory))
        {
            Directory.Delete(SaveDirectory, true);
        }

        BoardSessionState.Clear();
        EvidenceInventory.Instance.RestoreEvidence(Array.Empty<EvidenceData>());
        EvidenceInventory.Instance.RestoreNotes(new Dictionary<string, string>(), string.Empty);
        PlayerAssistanceSettings.ResetHelpProgress();
        PlayerPrefs.DeleteKey("crt.archive.unlocked");
        PlayerPrefs.DeleteKey("crt.archive.mounted");
        MarkOfficeContext();
    }

    private static void RestoreBoardData(SaveData data)
    {
        RestoreVectorRecords(data.cardPositions, BoardSessionState.CardPositions);
        RestoreVectorRecords(data.worldPhotoPositions, BoardSessionState.WorldPhotoPositions);
        foreach (StringPair pair in data.evidenceZones)
        {
            if (!string.IsNullOrWhiteSpace(pair.key))
            {
                BoardSessionState.EvidenceZones[pair.key] = pair.value;
            }
        }

        foreach (string key in data.connections)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                BoardSessionState.Connections.Add(key);
            }
        }

        foreach (string conclusion in data.unlockedConclusions)
        {
            if (!string.IsNullOrWhiteSpace(conclusion))
            {
                BoardSessionState.UnlockedConclusions.Add(conclusion);
            }
        }

        foreach (IntRecord slot in data.timelineSlots)
        {
            if (!string.IsNullOrWhiteSpace(slot.key))
            {
                BoardSessionState.TimelineSlots[slot.key] = slot.value;
            }
        }
    }

    private static List<EvidenceData> CreateEvidence(List<EvidenceRecord> records)
    {
        List<EvidenceData> evidence = new();
        if (records == null)
        {
            return evidence;
        }

        foreach (EvidenceRecord record in records)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.id))
            {
                continue;
            }

            EvidenceData data = ScriptableObject.CreateInstance<EvidenceData>();
            data.name = record.id + "_SavedEvidence";
            data.evidenceId = record.id;
            data.evidenceName = record.name;
            data.description = record.description;
            data.narrativeLine = record.narrativeLine;
            data.hintText = record.hintText;
            data.category = record.category;
            data.sourceSceneName = record.sourceScene;
            data.photoSprite = LoadSprite(record.photoFile);
            evidence.Add(data);
        }

        return evidence;
    }

    private static Dictionary<string, string> ToDictionary(List<StringPair> pairs)
    {
        Dictionary<string, string> result = new();
        if (pairs == null)
        {
            return result;
        }

        foreach (StringPair pair in pairs)
        {
            if (!string.IsNullOrWhiteSpace(pair.key))
            {
                result[pair.key] = pair.value ?? string.Empty;
            }
        }

        return result;
    }

    private static string SaveEvidencePhoto(EvidenceData data)
    {
        Texture2D texture = data != null && data.photoSprite != null ? data.photoSprite.texture : null;
        if (texture == null)
        {
            return string.Empty;
        }

        try
        {
            string fileName = SanitizeFileName(data.evidenceId) + ".png";
            string path = Path.Combine(PhotoDirectory, fileName);
            Directory.CreateDirectory(PhotoDirectory);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            return fileName;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[Save] No se pudo guardar foto de evidencia: " + exception.Message);
            return string.Empty;
        }
    }

    private static Sprite LoadSprite(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        string path = Path.Combine(PhotoDirectory, fileName);
        if (!File.Exists(path))
        {
            return null;
        }

        byte[] bytes = File.ReadAllBytes(path);
        Texture2D texture = new(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(bytes))
        {
            return null;
        }

        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
    }

    private static void AddVectorRecords(List<Vector2Record> records, Dictionary<string, Vector2> source)
    {
        foreach (KeyValuePair<string, Vector2> entry in source)
        {
            records.Add(new Vector2Record { key = entry.Key, x = entry.Value.x, y = entry.Value.y });
        }
    }

    private static void RestoreVectorRecords(List<Vector2Record> records, Dictionary<string, Vector2> target)
    {
        if (records == null)
        {
            return;
        }

        foreach (Vector2Record record in records)
        {
            if (!string.IsNullOrWhiteSpace(record.key))
            {
                target[record.key] = new Vector2(record.x, record.y);
            }
        }
    }

    private static void AddStringPairs(List<StringPair> records, Dictionary<string, string> source)
    {
        foreach (KeyValuePair<string, string> entry in source)
        {
            records.Add(new StringPair { key = entry.Key, value = entry.Value });
        }
    }

    private static string SanitizeFileName(string value)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }

        return value;
    }

    private static string SaveDirectory => Path.Combine(Application.persistentDataPath, "Saves");
    private static string PhotoDirectory => Path.Combine(SaveDirectory, "EvidencePhotos");
    private static string SavePath => Path.Combine(SaveDirectory, SaveFileName);
}
