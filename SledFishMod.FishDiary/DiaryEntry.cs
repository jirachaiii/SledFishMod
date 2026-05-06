using System.Text.Json.Serialization;

namespace SledFishMod.FishDiary;

/// <summary>
/// Per-fish diary record. Serialised to JSON by FishDiaryStore.
/// </summary>
public class DiaryEntry
{
    [JsonPropertyName("caught")]
    public bool Caught { get; set; }

    /// <summary>Total times this fish has been caught.</summary>
    [JsonPropertyName("catchCount")]
    public int CatchCount { get; set; }

    /// <summary>Longest individual catch in metres.</summary>
    [JsonPropertyName("biggestLength")]
    public float BiggestLength { get; set; }

    /// <summary>Whether a shiny version of this fish has ever been caught.</summary>
    [JsonPropertyName("hasShiny")]
    public bool HasShiny { get; set; }

    /// <summary>Set to true on first catch; cleared when the diary panel is opened.</summary>
    [JsonPropertyName("isNew")]
    public bool IsNew { get; set; }
}
