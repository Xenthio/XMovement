/// <summary>
/// A guided rocket projectile. Supports straight-line flight and crosshair-tracking mode.
/// Works with <see cref="RpgWeapon"/>'s laser-guided aim or as a dumb-fire projectile.
///
/// Assign <see cref="ExplosionPrefab"/> to the standard XenGameKit explosion prefab
/// (e.g. <c>/prefabs/engine/explosion_med.prefab</c>).
/// </summary>
public sealed class RpgProjectile : BaseProjectile
{
	[Property, Group( "RPG" )] public SoundEvent LoopingSound { get; set; }
	[Property, Group( "RPG" )] public float ExplosionRadius { get; set; } = 256f;
	[Property, Group( "RPG" )] public float ExplosionDamage { get; set; } = 150f;
	[Property, Group( "RPG" )] public float ExplosionForce  { get; set; } = 1.5f;
	[Property, Group( "RPG" )] public string ExplosionPrefab { get; set; } = "/prefabs/engine/explosion_med.prefab";

	SoundHandle _loopHandle;

	protected override void OnEnabled()
	{
		base.OnEnabled();

		if ( LoopingSound.IsValid() )
			_loopHandle = Sound.Play( LoopingSound, WorldPosition );

		if ( !IsProxy )
			Rigidbody.Gravity = false;
	}

	protected override void OnDisabled()
	{
		_loopHandle?.Stop();
	}

	protected override void OnUpdate()
	{
		if ( _loopHandle is not null )
			_loopHandle.Position = WorldPosition;
	}

	protected override void OnHit( Collision collision = default )
	{
		_loopHandle?.Stop();
		Explode();
	}

	void Explode()
	{
		var prefab = ResourceLibrary.Get<PrefabFile>( ExplosionPrefab );
		if ( prefab is null )
		{
			Log.Warning( $"RpgProjectile: Can't find explosion prefab '{ExplosionPrefab}'" );
			GameObject.Destroy();
			return;
		}

		var go = GameObject.Clone( prefab, new CloneConfig
		{
			Transform   = WorldTransform.WithScale( 1 ),
			StartEnabled = false
		} );

		if ( go.IsValid() )
		{
			go.RunEvent<RadiusDamage>( x =>
			{
				x.Radius             = ExplosionRadius;
				x.PhysicsForceScale  = ExplosionForce;
				x.DamageAmount       = ExplosionDamage;
				x.Attacker           = Instigator.IsValid() ? Instigator.GameObject : null;
				x.DamageTags        ??= new TagSet();
				x.DamageTags.Add( DamageTags.Explosion );
			}, FindMode.EverythingInSelfAndDescendants );

			go.Enabled = true;
			go.NetworkSpawn( true, null );
		}

		GameObject.Destroy();
	}

	/// <summary>
	/// Continuously steer toward <paramref name="target"/>. Called by <see cref="RpgWeapon"/>
	/// each frame while laser-guided mode is active.
	/// </summary>
	[Rpc.Host]
	internal void UpdateWithTarget( Vector3 target, float speed )
	{
		var direction     = (target - WorldPosition).Normal;
		var targetRot     = Rotation.LookAt( direction, Vector3.Up );
		WorldRotation     = Rotation.Slerp( WorldRotation, targetRot, Time.Delta * 6f );
		Rigidbody.Velocity = WorldTransform.Forward * (speed * 2f);
	}
}
