/// <summary>
/// Registers the .sent asset type with s&box's ResourceLibrary.
/// A .sent file is a JSON manifest pointing at a prefab with display metadata
/// (Title, Description, Category) — the standard way to publish spawnable entities
/// to the s&box cloud.
///
/// Ported from Sandbox game (MIT License) — identical API surface so cloud addons
/// built against Sandbox's ScriptedEntity work without changes.
/// </summary>
[AssetType( Name = "Sandbox Entity", Extension = "sent", Category = "Sandbox", Flags = AssetTypeFlags.NoEmbedding | AssetTypeFlags.IncludeThumbnails )]
public class ScriptedEntity : GameResource
{
	[Property] public PrefabFile Prefab { get; set; }
	[Property] public string Title { get; set; }
	[Property] public string Description { get; set; }

	/// <summary>
	/// Groups this entity under a named category in spawn menus (e.g. "Chair", "Weapon", "Npc").
	/// Leave blank for "Other".
	/// </summary>
	[Property] public string Category { get; set; }

	/// <summary>
	/// When true, include addon code when publishing this entity to the cloud.
	/// </summary>
	[Property] public bool IncludeCode { get; set; }

	/// <summary>
	/// When true, only show this entity in the spawn menu while running in the editor.
	/// </summary>
	[Property] public bool Developer { get; set; }
}
