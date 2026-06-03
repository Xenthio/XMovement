using Sandbox.Rendering;

/// <summary>
/// Rocket-propelled grenade / recoilless rifle.
/// Fires a physics projectile that explodes on impact.
/// Optionally guides the rocket toward the player's crosshair while holding fire (laser-guided mode).
///
/// Requires a <see cref="BaseProjectile"/>-derived component on <see cref="ProjectilePrefab"/>.
/// </summary>
public class RpgWeapon : BaseWeapon
{
	[Property, Group( "RPG" )] public float TimeBetweenShots { get; set; } = 2f;
	[Property, Group( "RPG" )] public GameObject ProjectilePrefab { get; set; }
	[Property, Group( "RPG" )] public float ProjectileSpeed { get; set; } = 1024f;

	[Property, Group( "Effects" )] public SoundEvent ShootSound { get; set; }

	/// <summary>
	/// When enabled, fired rockets continuously track toward the player's crosshair.
	/// Toggle with right-click.
	/// </summary>
	[Property, Group( "RPG" ), Sync, ClientEditable]
	public bool IsTrackedAim { get; set; } = false;

	[Sync( SyncFlags.FromHost )] RpgProjectile ActiveProjectile { get; set; }

	bool _hasFired;
	bool _waitingForReload;

	/// <summary>True while a live rocket is being guided toward the crosshair.</summary>
	public bool IsGuiding => IsTrackedAim && ActiveProjectile.IsValid();

	// ─── BaseWeapon overrides ─────────────────────────────────────────────────

	protected override float GetPrimaryFireRate() => TimeBetweenShots;

	public override bool CanSecondaryAttack() => false;

	public override void OnControl( Player player )
	{
		base.OnControl( player );

		if ( Input.Pressed( "attack2" ) )
			ToggleTrackedAim();

		// Guide active rocket toward crosshair
		if ( IsGuiding )
			ActiveProjectile.UpdateDirection( GetAimTarget() - ActiveProjectile.WorldPosition, ProjectileSpeed );

		// Auto-reload after attack1 released
		if ( _hasFired && Input.Released( "attack1" ) )
		{
			_hasFired = false;

			if ( IsGuiding )
				_waitingForReload = true;
			else if ( CanReload() )
				OnReloadStart();
		}

		if ( _waitingForReload && !IsGuiding )
		{
			_waitingForReload = false;
			if ( CanReload() )
				OnReloadStart();
		}
	}

	public override void PrimaryAttack()
	{
		if ( HasOwner && !TakeAmmo( 1 ) )
		{
			TryAutoReload();
			return;
		}

		AddShootDelay( TimeBetweenShots );

		if ( ViewModel.IsValid() )
			ViewModel.RunEvent<ViewModel>( x => x.OnAttack() );
		else if ( WorldModel.IsValid() )
			WorldModel.RunEvent<WorldModel>( x => x.OnAttack() );

		if ( ShootSound.IsValid() )
			GameObject.PlaySound( ShootSound );

		var ray       = AimRay;
		var muzzlePos = MuzzleTransform.WorldTransform.Position;
		var spawnPos  = muzzlePos + ray.Forward * 64f;

		if ( HasOwner )
		{
			spawnPos = ClampToLineOfSight( Owner, muzzlePos, spawnPos );

			// Recoil kick
			Owner.WalkController.EyeAngles += new Angles(
				Random.Shared.Float( -0.3f, -0.2f ),
				Random.Shared.Float( -0.1f,  0.1f ),
				0 );

			_hasFired = true;
		}

		CreateProjectile( spawnPos, ray.Forward, ProjectileSpeed );
	}

	public override void DrawHud( HudPainter painter, Vector2 crosshair )
	{
		const float w = 2f;
		painter.SetBlendMode( BlendMode.Lighten );

		if ( IsTrackedAim )
		{
			// Diamond crosshair when laser-guided
			var color = IsGuiding ? new Color( 1f, 0.5f, 0.1f ) : CrosshairCanShoot;
			var size  = 32f;
			painter.DrawLine( crosshair + new Vector2(  0,    -size ), crosshair + new Vector2(  size,  0    ), w, color );
			painter.DrawLine( crosshair + new Vector2(  size,  0    ), crosshair + new Vector2(  0,     size ), w, color );
			painter.DrawLine( crosshair + new Vector2(  0,     size ), crosshair + new Vector2( -size,  0    ), w, color );
			painter.DrawLine( crosshair + new Vector2( -size,  0    ), crosshair + new Vector2(  0,    -size ), w, color );
			return;
		}

		// Square crosshair
		var col  = CanPrimaryAttack() ? CrosshairCanShoot : CrosshairNoShoot;
		var half = 32f;
		painter.DrawLine( crosshair + new Vector2( -half, -half ), crosshair + new Vector2(  half, -half ), w, col );
		painter.DrawLine( crosshair + new Vector2(  half, -half ), crosshair + new Vector2(  half,  half ), w, col );
		painter.DrawLine( crosshair + new Vector2(  half,  half ), crosshair + new Vector2( -half,  half ), w, col );
		painter.DrawLine( crosshair + new Vector2( -half,  half ), crosshair + new Vector2( -half, -half ), w, col );
	}

	// ─── Helpers ─────────────────────────────────────────────────────────────

	[Rpc.Host]
	void ToggleTrackedAim() => IsTrackedAim = !IsTrackedAim;

	/// <summary>World point the local player's crosshair is aimed at.</summary>
	Vector3 GetAimTarget()
	{
		var ray = AimRay;
		var tr  = Scene.Trace.Ray( ray, 16384f )
			.IgnoreGameObjectHierarchy( AimIgnoreRoot )
			.WithoutTags( "trigger", "projectile" )
			.Run();
		return tr.Hit ? tr.HitPosition : ray.Position + ray.Forward * 16384f;
	}

	/// <summary>Clamp spawn position to avoid spawning the rocket inside geometry.</summary>
	Vector3 ClampToLineOfSight( Player player, Vector3 eye, Vector3 target )
	{
		var tr = Scene.Trace.Box( BBox.FromPositionAndSize( Vector3.Zero, 8f ), eye, target )
			.WithoutTags( "trigger", "ragdoll", "player", "effect" )
			.IgnoreGameObjectHierarchy( player.GameObject )
			.Run();
		return tr.Hit ? tr.EndPosition : target;
	}

	[Rpc.Host]
	void CreateProjectile( Vector3 start, Vector3 direction, float speed )
	{
		if ( !ProjectilePrefab.IsValid() )
		{
			Log.Warning( "RpgWeapon: no ProjectilePrefab assigned" );
			return;
		}

		var go         = ProjectilePrefab.Clone( start );
		var projectile = go.GetComponent<RpgProjectile>();

		if ( !projectile.IsValid() )
		{
			Log.Warning( "RpgWeapon: ProjectilePrefab has no RpgProjectile component" );
			go.Destroy();
			return;
		}

		if ( Owner.IsValid() )
			projectile.Instigator = Owner;

		projectile.Weapon = GameObject;
		go.NetworkSpawn();

		ActiveProjectile = projectile;
		projectile.UpdateDirection( direction, speed );
	}
}
