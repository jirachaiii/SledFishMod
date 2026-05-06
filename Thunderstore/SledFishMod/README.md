# SledFishMod

A framework mod for **Sledding Game** that lets anyone add custom fish (and other catchable things) without writing any code. Just drop a folder into `BepInEx/plugins/` and the mod picks it up automatically.

---

## For Players

Install this mod alongside any fish pack you want. SledFishMod itself adds nothing on its own — it's the engine that makes fish packs work.

### Installing a fish pack

1. Download a fish pack (e.g. **WebFishingFishPack**)
2. Drop the pack's folder into `BepInEx/plugins/`
3. Launch the game — the pack loads automatically

You can have as many packs installed at once as you like. **Everyone on the same server needs the same packs installed** for custom fish to work correctly in multiplayer.

---

## For Pack Creators

Creating a fish pack requires no code — just a JSON file and some PNG images.

### Folder structure

```
BepInEx/plugins/
  YourPackName/
    fish_pack.json
    FishImages/
      your_fish.png
      another_fish.png
```

### fish_pack.json format

```json
{
  "packId": "my_fish_pack",
  "packName": "My Fish Pack",
  "fish": [
    {
      "guid": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
      "name": "Space Bass",
      "rarity": "Rare",
      "minLength": 0.30,
      "maxLength": 5.00,
      "canBeShiny": true
    }
  ]
}
```

### Fields

| Field | Required | Description |
|-------|----------|-------------|
| `guid` | ✅ | A unique UUID for this fish. Generate one at [uuidgenerator.net](https://www.uuidgenerator.net/). **Never change this after publishing** — it's how the game identifies the fish. |
| `name` | ✅ | Display name shown in-game |
| `rarity` | ✅ | `Common`, `Uncommon`, `Rare`, `UltraRare`, or `Legendary` |
| `minLength` | ✅ | Minimum catch length in metres |
| `maxLength` | ✅ | Maximum catch length in metres |
| `canBeShiny` | ✅ | Whether the fish can be shiny (1% chance) |
| `spawnWeight` | ❌ | Override the default catch weight for this rarity (see below) |
| `image` | ❌ | Image filename inside `FishImages/` — defaults to `{Name}.png` |

### Default catch weights by rarity

| Rarity | Default weight | Approx. chance* |
|--------|---------------|-----------------|
| Common | 1.00 | ~1.7% |
| Uncommon | 0.50 | ~0.8% |
| Rare | 0.25 | ~0.4% |
| UltraRare | 0.15 | ~0.25% |
| Legendary | 0.08 | ~0.13% |

*Approximate with only the WebFishingFishPack installed. Adding more packs slightly dilutes all rates.

### Images

- PNG format, any size (will be scaled in-game)
- Named to match `{Name}.png` by default (spaces replaced with underscores)
- Or specify a custom filename with the `"image"` field

### GUIDs

Each fish needs a globally unique identifier. **Generate a new UUID for every fish** — never copy one from another pack. Duplicate GUIDs are detected on startup and the conflicting pack will be skipped with a clear error in the log.

### Catch rate dilution

Adding more fish to the pool slightly reduces every fish's individual catch rate. The mod logs all effective catch rates on startup (check `BepInEx/LogOutput.log`) so you can see exactly what your pack contributes.

---

## Compatibility

- Requires **BepInEx 6.0.0-be.755** (IL2CPP build) — [download here](https://builds.bepinex.dev/projects/bepinex_be/755/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.755+3fab71a.zip)
- Compatible with multiplayer — all players must have the same packs installed
