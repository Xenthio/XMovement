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

			// Match rigidbody path exactly
			var ejectDir = EjectTransform.WorldRotation.Forward * 250
			             + (EjectTransform.WorldRotation.Right + Vector3.Random * -0.35f) * 250;

			var go = prefab.Clone( new CloneConfig { Transform = EjectTransform.WorldTransform.WithScale( 1 ), StartEnabled = false } );

			if ( go.IsValid() )
			{
				var brass = go.Components.Get<BrassEjectPhysics>( FindMode.EverythingInSelfAndDescendants );
				if ( brass.IsValid() )
				{
					brass.StartVelocity        = ejectDir;
					brass.StartAngles          = EjectTransform.WorldRotation.Angles();
					brass.StartAngularVelocity = EjectTransform.WorldRotation.Right * 50f;
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

	public void DoMuzzleEffect()
	{
		if ( !MuzzleEffect.IsValid() ) return;
		if ( !MuzzleTransform.IsValid() ) return;

		var go = MuzzleEffect.Clone( new CloneConfig { Parent = MuzzleTransform, Transform = global::Transform.Zero, StartEnabled = true } );

		// Fallback: if the prefab has no TemporaryEffect, add one so it always cleans itself up
		if ( go.IsValid() && !go.Components.Get<TemporaryEffect>( FindMode.InSelf ) .IsValid() )
		{
			var te = go.Components.Create<TemporaryEffect>();
			te.DestroyAfterSeconds = 0.05f;
			te.WaitForChildEffects = true;
		}
	}

	public virtual void OnAttack() { }

	/// <summary>
	/// Called after a melee swing resolves. hasHit=true plays the solid-hit
	/// animation variant (b_attack_has_hit); false plays the miss/air swing.
	/// </summary>
	public virtual void OnMeleeAttack( bool hasHit ) { }

	/// <summary>
	/// Spawn ranged effects (tracer) from this model's muzzle point.
	/// RunEvent hits both ViewModel and WorldModel; WorldModel skips if a ViewModel is present
	/// so the tracer always originates from whichever model is actually visible.
	/// </summary>
	public virtual void CreateRangedEffects( BaseWeapon weapon, Vector3 hitPoint, Vector3? origin ) { }

}
