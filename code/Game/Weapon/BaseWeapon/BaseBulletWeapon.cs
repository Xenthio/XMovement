using Sandbox.Rendering;

/// <summary>
/// A hitscan bullet weapon. All stats are configurable via Properties —
/// most weapons need no C# subclass at all, just configure the prefab.
/// Actual bullet logic lives in <see cref="Bullet"/> and is usable from anywhere.
/// </summary>
public partial class BaseBulletWeapon : BaseWeapon
{
	[Property] public SoundEvent ShootSound { get; set; }

	[Property, Group( "Bullet" )] public float Damage { get; set; } = 12f;
	[Property, Group( "Bullet" )] public float BulletRadius { get; set; } = 1f;
	[Property, Group( "Bullet" )] public float Range { get; set; } = 4096f;
	[Property, Group( "Bullet" )] public float ShootForce { get; set; } = 80f;

	/// <summary>Base aim cone (degrees) at rest.</summary>
	[Property, Group( "Bullet" )] public Vector2 AimConeBase { get; set; } = new( 0.5f, 0.25f );
	/// <summary>Additional aim cone added when firing rapidly.</summary>
	[Property, Group( "Bullet" )] public Vector2 AimConeSpread { get; set; } = new( 3f, 3f );
	/// <summary>Seconds for aim cone to fully recover after a shot.</summary>
	[Property, Group( "Bullet" )] public float AimConeRecovery { get; set; } = 0.2f;

	[Property, Group( "Recoil" )] public Vector2 RecoilPitch { get; set; } = new( -0.3f, -0.1f );
	[Property, Group( "Recoil" )] public Vector2 RecoilYaw { get; set; } = new( -0.1f, 0.1f );

	/// <summary>Impact particle override. Null = use per-surface defaults.</summary>
	[Property, Group( "Effects" )] public GameObject ImpactEffectOverride { get; set; }

	/// <summary>Sound played near players when the bullet flies past them. Null = no flyby.</summary>
	[Property, Group( "Effects" )] public SoundEvent FlybySound { get; set; }

	protected TimeSince TimeSinceShoot;

	/// <summary>0 = no spread, 1 = full AimConeSpread.</summary>
	public float GetAimConeAmount() =>
		TimeSinceShoot.Relative.Remap( 0, AimConeRecovery, 1, 0 ).Clamp( 0, 1 );

	protected void ShootBullet( float aimConeScale = 1f )
	{
		if ( !TakeAmmo( 1 ) ) { TryAutoReload(); return; }

		AddShootDelay( GetPrimaryFireRate() );
		TimeSinceShoot = 0;

		var amount = GetAimConeAmount() * aimConeScale;
		var spread = AimConeBase.x + amount * AimConeSpread.x;

		var tr = Bullet.Fire( new BulletInfo
		{
			Origin               = AimRay.Position,
			Direction            = AimRay.Forward,
			Damage               = Damage,
			Radius               = BulletRadius,
			Range                = Range,
			Force                = ShootForce,
			Spread               = spread,
			Count                = 1,
			Attacker             = Owner?.GameObject,
			Weapon               = GameObject,
			ImpactEffectOverride = ImpactEffectOverride,
			FlybySound           = FlybySound,
		} );

		// Weapon-side effects: sound, attack anim, muzzleflash, tracer (per-model muzzle)
		BroadcastShootEffects( tr.EndPosition );

		if ( !HasOwner ) return;

		Owner.WalkController.EyeAngles += new Angles(
			Random.Shared.Float( RecoilPitch.x, RecoilPitch.y ),
			Random.Shared.Float( RecoilYaw.x, RecoilYaw.y ),
			0 );
	}

	/// <summary>
	/// Broadcasts weapon-side fire effects to all clients: shoot sound, attack animation, muzzleflash, tracer.
	/// Tracer is spawned per-model via CreateRangedEffects so each model uses its own muzzle point
	/// (viewmodel for first-person, worldmodel for third-person/spectators — matching sandbox behaviour).
	/// Subclasses can override OnShootEffects() for extras (e.g. casing eject, pump anim).
	/// </summary>
	[Rpc.Broadcast]
	protected void BroadcastShootEffects( Vector3 hitPoint )
	{
		if ( Application.IsDedicatedServer ) return;

		// Body anim
		Owner?.WalkController?.BodyModelRenderer?.Set( "b_attack", true );

		// Animation and effects from the active weapon model only:
		// First-person: viewmodel; Third-person/spectators: worldmodel
		var activeModel = WeaponModel;
		if ( activeModel.IsValid() )
		{
			// Anim + muzzle + eject from active model
			activeModel.GameObject.RunEvent<WeaponModel>( x => x.OnAttack() );
			// Tracers from active model
			activeModel.GameObject.RunEvent<WeaponModel>( x => x.CreateRangedEffects( this, hitPoint, null ) );
		}

		// Shoot sound — despatialised for the local shooter
		if ( ShootSound.IsValid() )
		{
			var snd = GameObject.PlaySound( ShootSound );
			if ( HasOwner && Owner.IsLocalPlayer && snd.IsValid() )
				snd.SpacialBlend = 0;
		}

		OnShootEffects();
	}

	/// <summary>
	/// Override to add extra weapon-specific shoot effects (e.g. casing eject, pump anim).
	/// Called client-side inside BroadcastShootEffects.
	/// </summary>
	protected virtual void OnShootEffects() { }

	public override void PrimaryAttack() => ShootBullet();

	public override void DrawCrosshair( HudPainter hud, Vector2 center )
	{
		var t = GetAimConeAmount();
		var gap = AimConeBase.x + t * AimConeSpread.x;
		var len = 8f;
		var w = 2f;
		var color = CanPrimaryAttack() ? CrosshairCanShoot : CrosshairNoShoot;

		hud.SetBlendMode( BlendMode.Lighten );
		hud.DrawLine( center + Vector2.Left  * (len + gap), center + Vector2.Left  * gap, w, color );
		hud.DrawLine( center - Vector2.Left  * (len + gap), center - Vector2.Left  * gap, w, color );
		hud.DrawLine( center + Vector2.Up    * (len + gap), center + Vector2.Up    * gap, w, color );
		hud.DrawLine( center - Vector2.Up    * (len + gap), center - Vector2.Up    * gap, w, color );
	}
}
