# AZ Multiplayer Core (`com.azeesoft.multiplayer-core`)

Lobby, transport switching, and spawn helpers for Netcode for GameObjects.

## Install

In the game project's `Packages/manifest.json` (path is relative to the `Packages` folder):

```json
"com.azeesoft.utils": "file:../../az-utils",
"com.azeesoft.multiplayer-core": "file:../../multiplayer-core"
```

Also add NGO, and the Steam / WebRTC transport packages you need. This package does not vendor those transports.

## What you get

- `CustomNetworkManager` — singleton + `UseTransport<T>()`
- `SimpleLobbyUI` prefab / `SimpleLobbyManager` — host/join, lobby code, player name, start game
- `SimplePlayerCharacterSpawner` — spawn a game-specific pawn per client
- `SteamCustomTransport`, `ClientNetworkAnimator`, `AutoHostOrJoinSession` (MPPM auto host/join in the Editor)
- `INetworkPlayerIdentity` — games implement this (see the starter's `PlayerNetworkIdentity`)
- `NetworkPushableObject` — server-authoritative rigidbody push via `Push_Rpc`
- **NetCodeGenerator** — Editor menu that writes `INetworkSerializable` + `IEquatable<T>` for marked structs

`NetworkManager`, the default network prefabs list, `PlayerNetworkIdentity`, and `PauseMenu` belong in the game project so you can edit them.

`com.unity.multiplayer.playmode` is a package dependency, so Unity installs it with this package.

Import the **Lobby** sample for a ready scene, or use the starter template. Set `gameSceneNames` to one scene for a standalone game, or several for a picker.

`PlayerCharacter` (third-person avatar) is not in this package — each game supplies its own pawn.

## Network serialization generator

NGO needs `INetworkSerializable` (and usually `IEquatable<T>`) on custom structs you send over the network. Mark a **`partial struct`**, then generate the implementation instead of writing it by hand.

Requires the **.NET 8 SDK** (`dotnet` on PATH). The Editor copies Unity / Netcode / Collections DLLs from this machine, builds the tool, and writes `Assets/Generated/*.g.cs`.

### Basic usage

1. Add the attribute (keep this namespace so existing data types keep compiling):

```csharp
using NetCodeGenerator.Serialization;
using Unity.Collections;
using UnityEngine;

[GenerateNetworkSerialization]
public partial struct PlayerSnapshot
{
    public int Health;
    public Vector3 Position;
    public UnityEngine.Quaternion Rotation;
    public FixedString64Bytes Name;
}
```

2. Save the file. Unity regenerates `Assets/Generated/*.g.cs` automatically. You can also run **Tools → AZ → Generate Network Serialization**.
3. Commit both the struct and the generated file. Do not edit the `.g.cs` files.

The struct **must** be `partial`. Classes are not supported.

### What the generator can serialize

| Field type | Notes |
|---|---|
| Primitives and enums | `int`, `float`, `bool`, custom enums, … |
| Unity value types | `Vector2/3/4`, `Quaternion`, `Color` / `Color32`, `Ray` / `Ray2D` |
| `FixedString*Bytes` | Prefer these over `string` |
| Nested `INetworkSerializable` structs | Including other `[GenerateNetworkSerialization]` structs |
| `Unity.Collections.FixedList*Bytes<T>` | Preferred for lists of unmanaged / nested generated structs |
| `List<T>` | Works, but **allocates**. Prefer `FixedList` in production |

Unsupported as fields: classes, `string`, dictionaries, arrays, and anything NGO cannot `SerializeValue`.

### Nested / complex structs

Every nested custom struct that goes on the wire needs its **own** attribute (or a hand-written `INetworkSerializable`). Generate from the **leaves inward** — one menu run processes every marked struct under `Assets`, so a tree of types is fine in a single pass.

```csharp
using NetCodeGenerator.Serialization;
using Unity.Collections;

[GenerateNetworkSerialization]
public partial struct InventoryItem
{
    public int Id;
    public int Count;
}

[GenerateNetworkSerialization]
public partial struct InventoryState
{
    public int Capacity;
    public FixedList128Bytes<InventoryItem> Items;
}
```

Rules of thumb:

- Put **unmanaged** nested data in `FixedList*Bytes<T>` (`int`, `Vector3`, other generated structs that only contain unmanaged fields).
- If a nested struct contains a `List<T>` (managed), it cannot live in a `FixedList`. Use `List<T>` on the parent, or flatten the layout.
- Mark each level. A field of type `InventoryItem` will not generate correctly if only the parent is attributed.
- Keep namespaces however you like; generated files stay in the same namespace as the struct.

### Output location

The runner scans **`Assets/`** only (not `Packages/` or `Library/`). Output is always `Assets/Generated/`. Add that folder to version control so other machines compile without running the tool.
