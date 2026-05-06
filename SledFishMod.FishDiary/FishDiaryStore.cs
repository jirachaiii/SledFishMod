using System.Text.Json;
using System.Text.Json.Serialization;
using BepInEx.Logging;

namespace SledFishMod.FishDiary;

/// <summary>
/// Loads and saves the fish diary JSON file.
/// Keys: custom fish use their GUID string; vanilla fish use "vanilla_{(int)FishType}".
/// </summary>
public static class FishDiaryStore
{
    private static string _savePath;
    private static Dictionary<string, DiaryEntry> _entries = new();
    private static ManualLogSource _log;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>
    /// Call once at plugin load. Locates the save file and reads existing data.
    /// </summary>
    public static void Init(string configDir, ManualLogSource log)
    {
        _log = log;
        _savePath = Path.Combine(configDir, "SledFishMod_FishDiary.json");
        Load();
        _log.LogInfo($"[FishDiary] Diary loaded — {_entries.Count} entries from {_savePath}");
    }

    // ── Public read/write ─────────────────────────────────────────────────

    /// <summary>All diary entries, keyed by fish key.</summary>
    public static IReadOnlyDictionary<string, DiaryEntry> AllEntries => _entries;

    /// <summary>Returns the entry for a key, or null if never seen.</summary>
    public static DiaryEntry GetEntry(string key) =>
        _entries.TryGetValue(key, out var e) ? e : null;

    /// <summary>
    /// Record a catch. Creates a new entry if necessary, updates stats, marks isNew on
    /// first catch, then persists to disk.
    /// </summary>
    public static void RecordCatch(string key, float length, bool isShiny)
    {
        if (!_entries.TryGetValue(key, out var entry))
            entry = new DiaryEntry();

        var firstCatch = !entry.Caught;
        entry.Caught = true;
        entry.CatchCount++;
        if (length > entry.BiggestLength) entry.BiggestLength = length;
        if (isShiny) entry.HasShiny = true;
        if (firstCatch) entry.IsNew = true; // cleared when diary panel is opened

        _entries[key] = entry;
        Save();
    }

    /// <summary>
    /// Clears the "NEW!" flag on all entries and saves. Call when the diary panel opens.
    /// </summary>
    public static void ClearNewFlags()
    {
        var changed = false;
        foreach (var key in _entries.Keys.ToList())
        {
            if (_entries[key].IsNew)
            {
                _entries[key].IsNew = false;
                changed = true;
            }
        }
        if (changed) Save();
    }

    // ── Serialisation helpers ─────────────────────────────────────────────

    private static void Load()
    {
        if (!File.Exists(_savePath))
        {
            _entries = new();
            return;
        }

        try
        {
            var json = File.ReadAllText(_savePath);
            var data = JsonSerializer.Deserialize<DiaryFile>(json, _jsonOptions);
            _entries = data?.Entries ?? new();
        }
        catch (Exception ex)
        {
            _log?.LogWarning($"[FishDiary] Failed to read diary file — starting fresh. ({ex.Message})");
            _entries = new();
        }
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_savePath)!);
            var data = new DiaryFile { Entries = _entries };
            var json = JsonSerializer.Serialize(data, _jsonOptions);
            File.WriteAllText(_savePath, json);
        }
        catch (Exception ex)
        {
            _log?.LogWarning($"[FishDiary] Failed to save diary: {ex.Message}");
        }
    }

    // ── JSON container ────────────────────────────────────────────────────

    private sealed class DiaryFile
    {
        [JsonPropertyName("entries")]
        public Dictionary<string, DiaryEntry> Entries { get; set; } = new();
    }
}
