// ================================================================
// SandboxCompat — PlayerController component stub
//
// Sandbox's Player has [RequireComponent] public PlayerController Controller.
// Addon code (e.g. ViewModelComponent, WorldModelComponent) does:
//   Scene.GetAllComponents<PlayerController>()
// to find the player and then searches its children for weapons.
//
// XenGameKit uses PlayerWalkControllerComplex instead of PlayerController.
// This stub Component named "PlayerController" sits on the Player GameObject
// so those GetAllComponents calls find something and can navigate up to Player.
//
// Delete this file if you don't need Sandbox addon compat.
// ================================================================

/// <summary>
/// Sandbox compat stub. Lives on the Player GameObject so that addon code
/// calling Scene.GetAllComponents&lt;PlayerController&gt;() can find the player
/// and navigate to its weapons/inventory.
///
/// Forwards the most-used Sandbox PlayerController properties to our
/// WalkController so addons that access them don't NPE.
/// </summary>
public sealed class PlayerController : Component
{
	[RequireComponent] public Player Player { get; set; }

	// ------------------------------------------------------------------
	// Properties addon weapons commonly read off PlayerController
	// ------------------------------------------------------------------

	public bool ThirdPerson =>
		Player?.WalkController?.CameraMode == XMovement.PlayerWalkControllerComplex.CameraModes.ThirdPerson;

	public Angles EyeAngles =>
		Player?.WalkController?.EyeAngles ?? default;

	public bool IsOnGround =>
		Player?.WalkController?.Controller?.IsOnGround ?? false;

	public Vector3 Velocity =>
		Player?.WalkController?.Controller?.Velocity ?? default;

	public bool IsNoclipping =>
		Player?.WalkController?.IsNoclipping ?? false;

	public bool IsCrouching =>
		Player?.WalkController?.IsCrouching ?? false;

	/// <summary>
	/// WishVelocity — used by some animation systems.
	/// </summary>
	public Vector3 WishVelocity =>
		Player?.WalkController?.Controller?.WishVelocity ?? default;

	/// <summary>
	/// Whether this is the local player's controller.
	/// </summary>
	public bool IsOwner => Player?.IsLocalPlayer ?? false;
}
