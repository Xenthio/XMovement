using XMovement;

public sealed partial class Player : Component, Component.IDamageable
{
	public static Player FindLocalPlayer() => Game.ActiveScene.GetAllComponents<Player>().Where( x => !x.IsProxy ).FirstOrDefault();

	[RequireComponent] public PlayerWalkControllerComplex WalkController { get; set; }
	[RequireComponent] public PlayerMovement Movement { get; set; }
	[Property] public GameObject Body { get; set; }

	[Property, Range( 0, 100 ), Sync( SyncFlags.FromHost )] public float Health { get; set; } = 100;
	[Property, Range( 0, 100 )] public float MaxHealth { get; set; } = 100;
	[Sync( SyncFlags.FromHost )] public float Armour { get; set; } = 0;
	[Property] public float MaxArmour { get; set; } = 100;

	[Sync( SyncFlags.FromHost )] public PlayerData PlayerData { get; set; }

	public bool IsLocalPlayer => !IsProxy;
	public bool IsDead => Health <= 0;
	public Guid PlayerId => PlayerData.PlayerId;
	public long SteamId => PlayerData.SteamId;
	public string DisplayName => PlayerData.DisplayName;

	public Transform EyeTransform => new( WalkController.Head.WorldPosition, WalkController.EyeAngles.ToRotation() );

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if ( !IsProxy )
			OnControl();
	}

	void OnControl()
	{
		if ( Input.Pressed( "die" ) )
		{
			Local.IPlayerEvents.PostToGameObject( GameObject, x => x.OnSuicide() );
			Global.IPlayerEvents.Post( x => x.OnPlayerSuicide( this ) );
			Health = 0;
			Kill( default );
		}
	}

	void Kill( DamageInfo dmg )
	{
		GameManager.Current?.OnDeath( this, dmg );
		Health = 0;

		var diedParams = new PlayerDiedParams
		{
			InstigatorId = dmg.InstigatorId,
			Attacker = dmg.Attacker,
		};

		// Fire OnDied — PlayerDeathEffect listens to this and handles ragdoll + respawn timing
		// Don't destroy the GO here; PlayerDeathEffect will handle cleanup after the death sequence
		Local.IPlayerEvents.PostToGameObject( GameObject, x => x.OnDied( diedParams ) );
		Global.IPlayerEvents.Post( x => x.OnPlayerDied( this, diedParams ) );
	}

	public void OnDamage( in Sandbox.DamageInfo damage )
	{
		if ( !Networking.IsHost ) return;
		if ( Health < 1 ) return;

		var dmg = damage as DamageInfo ?? new DamageInfo( damage.Damage, damage.Attacker, damage.Weapon );

		var damageEvent = new PlayerDamageEvent { Player = this, DamageInfo = dmg, Damage = dmg.Damage };
		Local.IPlayerEvents.PostToGameObject( GameObject, x => x.OnDamaging( damageEvent ) );
		Global.IPlayerEvents.Post( x => x.OnPlayerDamaging( damageEvent ) );

		if ( damageEvent.Cancelled ) return;

		var amount = damageEvent.Damage;

		if ( dmg.Tags.Contains( DamageTags.Headshot ) )
			amount *= 2f;

		if ( Armour > 0 )
		{
			float remaining = amount - Armour;
			Armour = Math.Max( 0, Armour - amount );
			amount = Math.Max( 0, remaining );
		}

		Health -= amount;

		NotifyOnDamage( new PlayerDamageParams
		{
			Damage = amount,
			InstigatorId = dmg.InstigatorId,
			Attacker = dmg.Attacker,
			Weapon = dmg.Weapon,
			Tags = dmg.Tags,
			Position = dmg.Position,
			Origin = dmg.Origin,
		} );

		if ( Health >= 1 ) return;

		Kill( dmg );
	}

	private SoundHandle _dmgSound;
	/// <summary>Minimum seconds between damage sounds of the same type to avoid fire-tick spam.</summary>
	private float _nextBurnSoundTime;
	private float _nextPainSoundTime;
	private const float BurnSoundInterval  = 0.75f;
	private const float PainSoundInterval  = 0.25f;

	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	private void NotifyOnDamage( PlayerDamageParams args )
	{
		Local.IPlayerEvents.PostToGameObject( GameObject, x => x.OnDamage( args ) );
		Global.IPlayerEvents.Post( x => x.OnPlayerDamage( this, args ) );

		if ( IsLocalPlayer )
		{
			var now = Time.Now;

			if ( args.Tags.Contains( DamageTags.Burn ) )
			{
				// Fire ticks constantly — throttle heavily and don't stop the current pain sound
				if ( now >= _nextBurnSoundTime )
				{
					_nextBurnSoundTime = now + BurnSoundInterval;
					_dmgSound?.Stop();
					_dmgSound = Sound.Play( "damage_taken_burn" );
				}
			}
			else if ( now >= _nextPainSoundTime )
			{
				_nextPainSoundTime = now + PainSoundInterval;
				_dmgSound?.Stop();
				_dmgSound = args.Tags.Contains( DamageTags.Shock )
					? Sound.Play( "damage_taken_shock" )
					: Sound.Play( "damage_taken_shot" );
			}
		}
	}

	/// <summary>
	/// Respawn this player in-place, restoring health and firing spawn events.
	/// If you want to teleport to a spawnpoint first, pass a transform — null keeps current position.
	/// </summary>
	[Rpc.Broadcast( NetFlags.HostOnly | NetFlags.Reliable )]
	public void Respawn( Transform? location = null )
	{
		Assert.True( Networking.IsHost, "Respawn must be called on the host" );

		Health = MaxHealth;
		Armour = 0;

		if ( location.HasValue )
			WorldTransform = location.Value;

		GameObject.Enabled = true;
		WalkController.Enabled = true;

		Local.IPlayerEvents.PostToGameObject( GameObject, x => x.OnSpawned() );
		GameRulesService.Current?.EquipPlayer( this );
	}

	public T GetWeapon<T>() where T : BaseCarryable
	{
		return Components.Get<PlayerInventory>()?.GetWeapon<T>();
	}

	public void SwitchWeapon<T>() where T : BaseCarryable
	{
		var weapon = GetWeapon<T>();
		if ( weapon is null ) return;
		Components.Get<PlayerInventory>()?.SwitchWeapon( weapon );
	}

}
