// Blood and gore decal system.
//
// CS2-inspired: spray particles on hit + persistent decals rescued from the
// particle lifetime so they stay on walls/floors until map cleanup.
//
// Usage:
//   BloodSystem.Splat( hitPosition, hitNormal, hitObject );  // bullet/melee on living thing
//   BloodSystem.Drip( position );                            // pooling under a corpse
//
// Configure DefaultBloodPrefabPath on a scene component or GameManager startup.
// Falls back to s&box's built-in flesh surface BulletImpact prefab if none set.
public static class BloodSystem
{
	// Scene-wide fallback path — set from GameManager or a scene component.
	public static string DefaultBloodPrefabPath { get; set; } = "prefabs/effects/blood_impact.prefab";

	// How long rescued decals persist on surfaces (seconds).
	private const float DecalLifetime = 45f;

	// Per-second budget: don't spawn more than this many blood effects per client per second.
	private static int _budgetSecond;
	private static int _budgetUsed;
	public static int MaxPerSecond { get; set; } = 64;

	/// <summary>
	/// Spray blood at a bullet/melee hit point. Call from host damage code.
	/// Broadcasts particles + decal to all clients.
	/// </summary>
	public static void Splat( Vector3 position, Vector3 normal, GameObject hitObject, string prefabPath = null )
	{
		SpawnBlood( position, normal, hitObject, prefabPath ?? DefaultBloodPrefabPath );
		NpcStimulusSystem.EmitSmell( position, "blood", intensity: 0.7f, source: hitObject );
	}

	/// <summary>
	/// Drip a small blood pool under a dying NPC/player. Parented to the object so it
	/// moves with ragdolls before settling. Call from OnDie / ragdoll spawn.
	/// </summary>
	public static void Drip( Vector3 position, GameObject parent = null )
	{
		SpawnDrip( position, parent );
	}

	// ─── Broadcast helpers ────────────────────────────────────────────────────

	[Rpc.Broadcast]
	static void SpawnBlood( Vector3 position, Vector3 normal, GameObject hitObject, string prefabPath )
	{
		if ( Application.IsDedicatedServer ) return;

		// Per-client budget gate
		if ( !TryConsumeBudget() ) return;

		var prefab = ResourceLibrary.Get<PrefabFile>( prefabPath );
		if ( prefab is null )
		{
			// Fall back: try the flesh surface's built-in BulletImpact prefab
			var flesh = Surface.FindByName( "flesh" );
			PrefabFile fallback = flesh?.PrefabCollection.BulletImpact ?? flesh?.GetBaseSurface()?.PrefabCollection.BulletImpact;
			if ( fallback is null ) return;

			var rot2 = Rotation.LookAt( normal * -1f, Vector3.Up );
			var go2 = fallback.Clone( new CloneConfig { Transform = new Transform( position, rot2 ), StartEnabled = true } );
			if ( hitObject.IsValid() ) go2.SetParent( hitObject, true );
			RescueDecals( go2 );
			return;
		}

		var rot = Rotation.LookAt( normal * -1f, Vector3.Up );
		var impact = prefab.Clone( new CloneConfig { Transform = new Transform( position, rot ), StartEnabled = true } );
		if ( hitObject.IsValid() ) impact.SetParent( hitObject, true );

		// Detach any Decal children so they survive beyond the particle's TemporaryEffect lifetime
		RescueDecals( impact );
	}

	[Rpc.Broadcast]
	static void SpawnDrip( Vector3 position, GameObject parent )
	{
		if ( Application.IsDedicatedServer ) return;
		if ( !TryConsumeBudget() ) return;

		// Use a random blood splatter decal directly — no particle, just a floor pool
		var flesh = Surface.FindByName( "flesh" );
		var prefab = flesh?.PrefabCollection.BulletImpact ?? flesh?.GetBaseSurface()?.PrefabCollection.BulletImpact;
		if ( prefab is null ) return;

		// Flat on the floor
		var rot = Rotation.LookAt( Vector3.Down, Vector3.Forward );
		var go = prefab.Clone( new CloneConfig { Transform = new Transform( position, rot ), StartEnabled = true } );
		if ( parent.IsValid() ) go.SetParent( parent, true );
		RescueDecals( go );
	}

	// ─── Decal rescue ─────────────────────────────────────────────────────────
	// CS2-style: particles are short-lived (2-3s) but the decals they carry must
	// persist on the wall/floor. We detach them from the particle tree before it
	// gets destroyed so they outlive it.

	static void RescueDecals( GameObject root )
	{
		if ( !root.IsValid() ) return;

		var decals = new List<GameObject>();
		CollectDecals( root, decals );

		foreach ( var d in decals )
		{
			d.SetParent( null ); // reparent to scene root, preserving world transform
			if ( d.Components.TryGet<Decal>( out var decal ) )
			{
				decal.Transient = true;
				decal.LifeTime  = DecalLifetime;
			}
			// Auto-destroy after lifetime in case Decal doesn't handle it
			d.Components.GetOrCreate<TemporaryEffect>();
		}
	}

	static void CollectDecals( GameObject go, List<GameObject> result )
	{
		if ( !go.IsValid() ) return;
		if ( go.Components.Get<Decal>() != null ) { result.Add( go ); return; }
		foreach ( var child in go.Children.ToList() )
			CollectDecals( child, result );
	}

	// ─── Budget ───────────────────────────────────────────────────────────────

	static bool TryConsumeBudget()
	{
		int sec = (int)Time.Now;
		if ( sec != _budgetSecond ) { _budgetSecond = sec; _budgetUsed = 0; }
		if ( _budgetUsed >= MaxPerSecond ) return false;
		_budgetUsed++;
		return true;
	}
}
