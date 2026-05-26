public static class WeaponConVars
{
	/// <summary>
	/// 0 = normal, 1 = infinite ammo (clips still deplete), 2 = infinite ammo (no clip depletion)
	/// Mirrors Source's sv_infinite_ammo behaviour.
	/// </summary>
	[ConVar( "sv_infinite_ammo", ConVarFlags.Replicated, Help = "0: normal, 1: infinite reserves, 2: unlimited ammo (no depletion)" )]
	public static int InfiniteAmmo { get; set; } = 0;

	public static bool UnlimitedAmmo => InfiniteAmmo >= 2;
	public static bool InfiniteReserves => InfiniteAmmo >= 1;

	/// <summary>
	/// Use cheap particle-based brass ejection instead of full rigidbody physics.
	/// The EjectBrass property on WeaponModel must point to a prefab with a
	/// ParticleEffect + BrassEjectPhysics component for this to take effect.
	/// </summary>
	[ConVar( "cl_ejectbrass", Help = "0: off, 1: rigidbody (default), 2: cheap particle physics" )]
	public static int EjectBrass { get; set; } = 1;
}
