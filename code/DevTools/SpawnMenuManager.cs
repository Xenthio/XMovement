// ================================================================
// [DEBUG TOOL] Addon Spawn Test Menu — SpawnMenuManager
//
// This is debug / QA tooling for testing addon compatibility with
// XenGameKit. Most shipped games do NOT need this.
//
// To remove this feature entirely, delete:
//   code/DevTools/SpawnMenuManager.cs       (this file)
//   code/DevTools/AddonSpawnMenu.razor
//   code/DevTools/AddonSpawnMenu.razor.scss
// ================================================================

/// <summary>
/// Manages the debug spawn menu's open state, ConCmds, and cloud spawn dispatch.
/// Actual spawn logic lives in SandboxCompat/SandboxCompatSpawner.cs.
/// Panel is created via ISceneStartup.OnClientInitialize attached to the ScreenPanel.
/// </summary>
public sealed class SpawnMenuManager : GameObjectSystem<SpawnMenuManager>, ISceneStartup
{
	// ------------------------------------------------------------------
	// Open state — static so Razor can read it without a component ref
	// ------------------------------------------------------------------

	public static bool IsOpen { get; set; } = false;

	// ------------------------------------------------------------------
	// Toggle ConCmds (client-side, no flags)
	// ------------------------------------------------------------------

	[ConCmd( "spawnmenu_toggle" )]
	public static void ToggleConCmd() => IsOpen = !IsOpen;

	[ConCmd( "spawnmenu_open" )]
	public static void OpenConCmd() => IsOpen = true;

	[ConCmd( "spawnmenu_close" )]
	public static void CloseConCmd() => IsOpen = false;

	// ------------------------------------------------------------------
	// Cloud spawn ConCmd (server-side)
	// Usage: devtools_spawn_cloud prop|entity <ident>
	// Delegates to SandboxCompatSpawner — see that file for limitations.
	// ------------------------------------------------------------------

	[ConCmd( "devtools_spawn_cloud", ConVarFlags.Server | ConVarFlags.Cheat )]
	public static async void SpawnCloudCmd( Connection source, string type, string ident )
	{
		var player = Game.ActiveScene.GetAll<Player>()
			.FirstOrDefault( p => p.Network.Owner == source );

		if ( !player.IsValid() )
		{
			Log.Warning( "[SpawnMenu] devtools_spawn_cloud: no player found for caller" );
			return;
		}

		if ( type == "prop" )
			await SandboxCompatSpawner.SpawnCloudProp( player, ident );
		else if ( type == "entity" )
			await SandboxCompatSpawner.SpawnCloudEntity( player, ident );
		else
			Log.Warning( $"[SpawnMenu] Unknown spawn type '{type}'" );
	}

	// ------------------------------------------------------------------
	// Constructor / lifecycle
	// ------------------------------------------------------------------

	public SpawnMenuManager( Scene scene ) : base( scene ) { }

	void ISceneStartup.OnHostInitialize() { }

	void ISceneStartup.OnClientInitialize()
	{
		var screenPanel = Scene.GetAllComponents<ScreenPanel>().FirstOrDefault();
		if ( screenPanel is not null )
		{
			screenPanel.GameObject.Components.Create<XenGameKit.DevTools.AddonSpawnMenu>();
			Log.Info( "[SpawnMenu] Panel attached. Use 'spawnmenu_toggle' to open." );
		}
		else
		{
			Log.Warning( "[SpawnMenu] No ScreenPanel found — spawn menu will not be visible." );
		}
	}
}
