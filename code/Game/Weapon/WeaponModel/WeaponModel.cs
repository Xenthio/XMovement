using System.Threading;

public abstract class WeaponModel : Component
{
	[Property] public SkinnedModelRenderer Renderer { get; set; }
	[Property] public SoundEvent DeploySound { get; set; }
	[Property] public GameObject MuzzleTransform { get; set; }
	[Property] public GameObject EjectTransform { get; set; }
	[Property] public GameObject MuzzleEffect { get; set; }
	[Property] public GameObject EjectBrass { get; set; }
	[Property] public GameObject TracerEffect { get; set; }

	/// <summary>
	/// Prefab to spawn as a dropped magazine during reload.
	/// Should be a small physics prop with the mag mesh + Rigidbody.
	/// </summary>
	[Property] public GameObject MagazineDropPrefab { get; set; }

	/// <summary>
	/// Where the magazine detaches from (attach point on the weapon model).
	/// </summary>
	[Property] public GameObject MagazineTransform { get; set; }

	/// <summary>
	/// How many seconds into the reload animation to drop the magazine.
	/// </summary>
	[Property] public float MagazineDropTime { get; set; } = 0.2f;

	public void Deploy()
	{
		Renderer?.Set( "b_deploy", true );

		if ( DeploySound is not null )
			GameObject.PlaySound( DeploySound );
	}

	public Transform GetTracerOrigin()
	{
		if ( MuzzleTransform.IsValid() )
			return MuzzleTransform.WorldTransform;

		return WorldTransform;
	}

	public void DoTracerEffect( Vector3 hitPoint, Vector3? origin = null )
	{
		if ( !TracerEffect.IsValid() ) return;

		var tracerOrigin = GetTracerOrigin().WithScale( 1 );
		if ( origin.HasValue ) tracerOrigin = tracerOrigin.WithPosition( origin.Value );

		var effect = TracerEffect.Clone( new CloneConfig { Transform = tracerOrigin, StartEnabled = true } );

		if ( effect.GetComponentInChildren<Tracer>() is Tracer tracer )
			tracer.EndPoint = hitPoint;
	}

	public void DoEjectBrass()
	{
		if ( WeaponConVars.EjectBrass == 0 ) return;

		if ( !EjectTransform.IsValid() ) return;

		// cl_ejectbrass 2 = cheap particle physics
		if ( WeaponConVars.EjectBrass == 2 )
		{
			// Auto-look for <name>_cheap.prefab via ResourceLibrary
			GameObject cheapPrefab = null;
			if ( EjectBrass.IsValid() )
			{
				// Try to find the cheap variant by searching for a prefab whose
				// root object name matches <this prefab name>_cheap
				var cheapName = EjectBrass.Name + "_cheap";
				var cheapFile = ResourceLibrary.GetAll<PrefabFile>()
					.FirstOrDefault( p => p.RootObject?["Name"]?.ToString() == cheapName );
				if ( cheapFile is not null )
					cheapPrefab = GameObject.GetPrefab( cheapFile.ResourcePath );
			}
			var prefab = cheapPrefab.IsValid() ? cheapPrefab : EjectBrass;
			if ( !prefab.IsValid() ) return;

			// Inherit owner velocity
			var ownerVelocity = Vector3.Zero;
			var ownerCC = GameObject.Root.Components.Get<CharacterController>( FindMode.EverythingInSelfAndDescendants );
			if ( ownerCC.IsValid() ) ownerVelocity = ownerCC.Velocity;

			// TTT-style: local eject direction transformed to world space
			var ejectDir = EjectTransform.WorldRotation.Forward * 250
			             + (EjectTransform.WorldRotation.Right + Vector3.Random * -0.35f) * 250
			             + EjectTransform.WorldRotation.Up * Game.Random.Float( 0f, 64f )
			             + ownerVelocity;

			var go = prefab.Clone( new CloneConfig { Transform = EjectTransform.WorldTransform, StartEnabled = false } );

			if ( go.IsValid() )
			{
				// Set velocity before enabling — applied on first OnParticleStep, not OnParticleCreated
				var brass = go.Components.Get<BrassEjectPhysics>( FindMode.EverythingInSelfAndDescendants );
				if ( brass.IsValid() )
				{
					brass.StartVelocity        = ejectDir;
					brass.StartAngles          = EjectTransform.WorldRotation.Angles();
					brass.StartAngularVelocity = Vector3.Random * 300f;
				}
				go.Transform.ClearInterpolation();
				go.Enabled = true;
			}
			return;
		}

		// cl_ejectbrass 1 = full rigidbody (default)
		if ( !EjectBrass.IsValid() ) return;

		var effect = EjectBrass.Clone( new CloneConfig { Transform = EjectTransform.WorldTransform.WithScale( 1 ), StartEnabled = true } );

		var ejectDirection = EjectTransform.WorldRotation.Forward * 250
		                   + (EjectTransform.WorldRotation.Right + Vector3.Random * -0.35f) * 250;

		var rb = effect.GetComponentInChildren<Rigidbody>();
		if ( rb.IsValid() )
		{
			rb.Velocity        = ejectDirection;
			rb.AngularVelocity = EjectTransform.WorldRotation.Right * 50f;
		}
	}

	/// <summary>
	/// Drops a magazine prefab at the magazine attachment point.
	/// Called automatically from OnReloadStart after MagazineDropTime seconds.
	/// </summary>
	public async void DoDropMagazine( CancellationToken ct = default )
	{
		if ( !MagazineDropPrefab.IsValid() ) return;
		if ( MagazineDropTime > 0 )
		{
			try { await GameTask.DelaySeconds( MagazineDropTime, ct ); }
			catch { return; }
		}
		if ( ct.IsCancellationRequested ) return;

		var spawnAt = MagazineTransform.IsValid()
			? MagazineTransform.WorldTransform
			: WorldTransform;

		var mag = MagazineDropPrefab.Clone( new CloneConfig
		{
			Transform = spawnAt.WithScale( 1 ),
			StartEnabled = true
		} );

		// Give it a gentle downward + forward toss
		if ( mag.GetComponentInChildren<Rigidbody>() is { } rb )
		{
			rb.Velocity = spawnAt.Rotation.Down * 60f + spawnAt.Rotation.Forward * 20f;
			rb.AngularVelocity = Vector3.Random * 8f;
		}
	}

	public void DoMuzzleEffect()
	{
		if ( !MuzzleEffect.IsValid() ) return;
		if ( !MuzzleTransform.IsValid() ) return;

		var go = MuzzleEffect.Clone( new CloneConfig { Parent = MuzzleTransform, Transform = global::Transform.Zero, StartEnabled = true } );

		// Fallback: if the prefab has no TemporaryEffect, add one so it always cleans itself up
		if ( go.IsValid() && !go.Components.Get<TemporaryEffect>( FindMode.InSelf ) .IsValid() )
		{
			var te = go.Components.Create<TemporaryEffect>();
			te.DestroyAfterSeconds = 0.5f;
			te.WaitForChildEffects = false;
		}
	}

	public virtual void OnAttack() { }

	/// <summary>
	/// Called after a melee swing resolves. hasHit=true plays the solid-hit
	/// animation variant (b_attack_has_hit); false plays the miss/air swing.
	/// </summary>
	public virtual void OnMeleeAttack( bool hasHit ) { }

	public virtual void CreateRangedEffects( BaseWeapon weapon, Vector3 hitPoint, Vector3? origin ) { }
}
