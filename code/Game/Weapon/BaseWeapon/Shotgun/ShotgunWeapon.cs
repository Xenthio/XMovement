/// <summary>
/// Shotgun — fires multiple pellets per shot with wide spread.
/// Inherits from IronSightsWeapon to get ADS + aim cone mechanics.
/// </summary>
public sealed class ShotgunWeapon : IronSightsWeapon
{
	[Property, Group( "Shotgun" )] public int PelletCount { get; set; } = 8;
	[Property, Group( "Shotgun" )] public float PelletSpreadMultiplier { get; set; } = 1.5f;

	/// <summary>Semi-auto: fires on button press, not held.</summary>
	protected override bool WantsPrimaryAttack() => Input.Pressed( "attack1" );

	public override void PrimaryAttack()
	{
		if ( !TakeAmmo( 1 ) )
		{
			TryAutoReload();
			return;
		}

		AddShootDelay( GetPrimaryFireRate() );
		TimeSinceShoot = 0;

		// Fire multiple pellets and track all hit points for tracer effects
		var aimConeScale = IsAiming ? AimScale : 1f;
		_lastPelletHits.Clear();
		
		for ( var i = 0; i < PelletCount; i++ )
		{
			var amount = GetAimConeAmount() * aimConeScale;
			var spread = (AimConeBase.x + amount * AimConeSpread.x) * PelletSpreadMultiplier;

			var tr = Bullet.Fire( new BulletInfo
			{
				Origin = AimRay.Position,
				Direction = AimRay.Forward,
				Damage = Damage,
				Radius = BulletRadius,
				Range = Range,
				Force = ShootForce,
				Spread = spread,
				Count = 1,
				Attacker = Owner?.GameObject,
				Weapon = GameObject,
				ImpactEffectOverride = ImpactEffectOverride,
			} );
			
			_lastPelletHits.Add( tr.EndPosition );
		}

		// Broadcast effects (sound, anim, muzzleflash)
		BroadcastShootEffects( _lastPelletHits.Count > 0 ? _lastPelletHits[0] : AimRay.Position );

		if ( !HasOwner ) return;

		Owner.WalkController.EyeAngles += new Angles(
			Random.Shared.Float( RecoilPitch.x, RecoilPitch.y ),
			Random.Shared.Float( RecoilYaw.x, RecoilYaw.y ),
			0 );
	}

	private List<Vector3> _lastPelletHits = new();

	/// <summary>Called after BroadcastShootEffects to add extra effects like multiple tracers.</summary>
	protected override void OnShootEffects()
	{
		// Fire tracers for each pellet that hit
		// The active WeaponModel (set in BroadcastShootEffects) already picks the right one for camera view
		var activeModel = WeaponModel;
		if ( !activeModel.IsValid() ) return;

		foreach ( var hitPoint in _lastPelletHits )
			activeModel.GameObject.RunEvent<WeaponModel>( x => x.DoTracerEffect( hitPoint, null ) );
	}
}
