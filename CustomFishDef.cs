using System.Text.Json.Serialization;

namespace SledFishMod;

public class CustomFishDef
{
    [JsonPropertyName("guid")]
    public string Guid { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>
    /// Optional image filename (e.g. "alligator.png") inside the pack's FishImages/ folder.
    /// Defaults to "{Name}.png" (spaces replaced with underscores) if omitted.
    /// </summary>
    [JsonPropertyName("image")]
    public string Image { get; set; } = null;

    [JsonPropertyName("rarity")]
    public string Rarity { get; set; } = "Common";

    [JsonPropertyName("minLength")]
    public float MinLength { get; set; } = 0.20f;

    [JsonPropertyName("maxLength")]
    public float MaxLength { get; set; } = 0.80f;

    /// <summary>
    /// Optional spawn weight override. If omitted, the framework uses the rarity default:
    /// Common=1.00, Uncommon=0.50, Rare=0.25, UltraRare=0.15, Legendary=0.08
    /// </summary>
    [JsonPropertyName("spawnWeight")]
    public float? SpawnWeight { get; set; }

    [JsonPropertyName("canBeShiny")]
    public bool CanBeShiny { get; set; } = true;

    // --- Set at load time, not from JSON ---

    [JsonIgnore]
    public int RuntimeId { get; set; }

    [JsonIgnore]
    public string PackId { get; set; } = "";

    [JsonIgnore]
    public string ImagePath { get; set; } = "";

    /// <summary>Resolved spawn weight: explicit override or rarity default.</summary>
    [JsonIgnore]
    public float ResolvedSpawnWeight { get; set; }
}

public class FishDatabase
{
    [JsonPropertyName("packId")]
    public string PackId { get; set; } = "unknown_pack";

    [JsonPropertyName("packName")]
    public string PackName { get; set; } = "Unknown Pack";

    [JsonPropertyName("fish")]
    public List<CustomFishDef> Fish { get; set; } = new();
}
