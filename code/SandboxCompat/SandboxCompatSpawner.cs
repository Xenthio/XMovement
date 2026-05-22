// ================================================================
// SandboxCompat — cloud prop/entity spawn helpers
//
// ScriptedEntity is defined in ScriptedEntity.cs in this folder,
// so ResourceLibrary and Cloud.Load<ScriptedEntity> work correctly.
// ================================================================

public static class SandboxCompatSpawner
{
	// ------------------------------------------------------------------
	// Prop spawning (type:model packages)
	// ------------------------------------------------------------------

	public static async Task SpawnCloudProp( Player player, string ident )
	{
		var model = await Cloud.Load<Model>( ident );
		if ( model is null )
		{
			Log.Warning( $"[SandboxCompat] Could not load prop model '{ident}'" );
			return;
		}

		var go = new GameObject( false, model.ResourceName ?? ident );
		go.WorldTransform = GetSpawnTransform( player );

		var prop = go.AddComponent<Prop>();
		prop.Model = model;

		if ( (model.Physics?.Parts?.Count ?? 0) == 0 )
		{
			go.AddComponent<Rigidbody>();
			var col = go.AddComponent<BoxCollider>();
			col.Scale = model.Bounds.Size;
			col.Center = model.Bounds.Center;
		}

		go.NetworkSpawn();
		Log.Info( $"[SandboxCompat] Spawned prop '{ident}'" );
	}

	// ------------------------------------------------------------------
	// Entity spawning (type:sent packages)
	// ------------------------------------------------------------------

	public static async Task SpawnCloudEntity( Player player, string ident )
	{
		// Cloud.Load<ScriptedEntity> mounts the package and loads the .sent resource.
		// This works because we define ScriptedEntity with [AssetType(Extension="sent")]
		// in this assembly, which registers the extension with ResourceLibrary.
		var entity = await Cloud.Load<ScriptedEntity>( ident, true );

		if ( entity?.Prefab is null )
		{
			Log.Warning( $"[SandboxCompat] Could not load .sent entity '{ident}'" );
			return;
		}

		var prefab = GameObject.GetPrefab( entity.Prefab.ResourcePath );
		if ( prefab is null )
		{
			Log.Warning( $"[SandboxCompat] .sent entity '{ident}' has prefab '{entity.Prefab.ResourcePath}' but it could not be loaded" );
			return;
		}

		var go = prefab.Clone( new CloneConfig
		{
			Transform    = GetSpawnTransform( player ),
			StartEnabled = false
		} );
		go.NetworkSpawn();
		Log.Info( $"[SandboxCompat] Spawned entity '{entity.Title ?? ident}'" );
	}

	// ------------------------------------------------------------------
	// Helpers
	// ------------------------------------------------------------------

	public static Transform GetSpawnTransform( Player player )
	{
		var eyes = player.EyeTransform;
		var tr = Game.ActiveScene.Trace
			.Ray( eyes.Position, eyes.Position + eyes.Forward * 256f )
			.IgnoreGameObjectHierarchy( player.GameObject )
			.WithoutTags( "player", "trigger" )
			.Run();

		var pos = tr.Hit ? tr.HitPosition : eyes.Position + eyes.Forward * 80f;
		return new Transform( pos, Rotation.FromYaw( eyes.Rotation.Yaw() ) );
	}
}
