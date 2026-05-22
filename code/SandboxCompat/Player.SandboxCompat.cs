// ================================================================
// SandboxCompat — Player partial for Sandbox addon compat
//
// Adds a PlayerController component to the Player GameObject so that:
//   1. player.Controller works (matches Sandbox's Player.Controller API)
//   2. Scene.GetAllComponents<PlayerController>() finds players
//      (used by addon ViewModelComponent/WorldModelComponent to locate weapons)
//
// Delete this file if you don't need Sandbox addon weapon compat.
// ================================================================

public sealed partial class Player
{
	/// <summary>
	/// Sandbox compat: addon code calls player.Controller or
	/// Scene.GetAllComponents&lt;PlayerController&gt;() to find the player.
	/// This component lives on the Player GameObject and satisfies both.
	/// </summary>
	[RequireComponent] public PlayerController Controller { get; set; }
}
