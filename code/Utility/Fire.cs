/// <summary>
/// Static fire system. Call FireSystem.Ignite() to add fire to any GameObject.
/// Spawns the project-local override of /prefabs/engine/ignite.prefab as pure visuals
/// (particles + sound) and attaches <see cref="FireComponent"/> directly to the burning
/// object. That way the logic is destroyed automatically when the object dies — no
/// orphaned invisible damage dealers possible.
/// </summary>
public static class FireSystem
{
	public static FireComponent Ignite( GameObject go )
	{
		if ( !go.IsValid() ) return null;

		// Re-use an existing FireComponent if already burning
		var fire = go.GetComponent<FireComponent>( true );
		if ( fire.IsValid() )
		{
			fire.AddSelfHeat( fire.MaxHeat );
			return fire;
		}

		// Spawn the ignite prefab as pure visuals (particles + sound).
		// FireComponent is attached to `go` directly below, so it dies with `go`.
		var prefab = ResourceLibrary.Get<PrefabFile>( "/prefabs/engine/ignite.prefab" );
		if ( prefab == null )
		{
			Log.Warning( "FireSystem.Ignite: can't find /prefabs/engine/ignite.prefab" );
			return null;
		}

		var visualGo = GameObject.Clone( prefab, new CloneConfig { Parent = null, Transform = new global::Transform( go.WorldPosition ), StartEnabled = true } );

		// The prefab may contain a FireComponent (legacy setup) — strip it so logic
		// is owned exclusively by the component we add to `go` below.
		foreach ( var stray in visualGo.GetComponentsInChildren<FireComponent>( true ).ToList() )
			stray.Destroy();

		// Wire all ParticleModelEmitters to target the burning GO
		visualGo.RunEvent<ParticleModelEmitter>( x => x.Target = go );

		// Add FireComponent directly onto the burning object.
		// When `go` is destroyed → OnDestroy fires → visuals shut down. No polling needed.
		fire = go.Components.Create<FireComponent>();
		fire._igniteInstance = visualGo;

		return fire;
	}

	public static void Extinguish( GameObject go )
	{
		if ( !go.IsValid() ) return;

		var fire = go.GetComponent<FireComponent>();
		if ( fire.IsValid() )
			fire.Extinguish( fire.MaxHeat );
	}

	internal static void AddHeat( FireComponent fire, float heat, bool selfHeat )
	{
		if ( !fire.IsValid() ) return;
		if ( !fire.Enabled ) return;
		if ( heat <= 0f ) return;

		if ( !selfHeat && fire.IsBurning )
			heat *= fire.IncomingHeatScale;

		var startBurning = fire.HeatLevel <= 0f;

		if ( fire.CurrentHeatAbsorb > 0f && fire.AbsorbRate > 0f )
		{
			var absorbDamage = heat * fire.AbsorbRate;
			if ( absorbDamage > fire.CurrentHeatAbsorb )
			{
				heat -= fire.CurrentHeatAbsorb / fire.AbsorbRate;
				fire.CurrentHeatAbsorb = 0f;
			}
			else
			{
				fire.CurrentHeatAbsorb -= absorbDamage;
				heat = 0f;
			}
		}

		fire.HeatLevel = MathF.Min( fire.MaxHeat, fire.HeatLevel + heat );

		if ( startBurning && fire.HeatLevel > 0f )
			fire.SetBurningState( true );
	}

	internal static void DoExtinguish( FireComponent fire, float heat )
	{
		if ( !fire.IsValid() ) return;
		if ( !fire.Enabled ) return;
		if ( heat <= 0f ) return;

		fire.HeatLevel -= heat;
		fire.CurrentHeatAbsorb = MathF.Min( fire.MaxHeatAbsorb, fire.CurrentHeatAbsorb + fire.ExtinguishAbsorbScale * heat );

		if ( fire.HeatLevel <= 0f )
		{
			fire.HeatLevel = 0f;
			fire.SetBurningState( false );
		}
	}

	internal static void Update( FireComponent fire, float dt )
	{
		if ( !fire.IsValid() ) return;
		if ( !fire.Enabled ) return;
		if ( !Networking.IsHost ) return;
		if ( dt <= 0f ) return;

		if ( !fire.InfiniteFuel )
		{
			fire.RemainingFuel -= dt;
			if ( fire.RemainingFuel <= 0f )
			{
				DoExtinguish( fire, fire.MaxHeat );
				return;
			}
		}

		var addedHeat = fire.AttackTime > 0f
			? fire.MaxHeat / fire.AttackTime
			: fire.MaxHeat;

		addedHeat *= dt * fire.GrowthRate;
		AddHeat( fire, addedHeat, true );

		if ( !fire.IsBurning )
			return;

		var strength = fire.GetHeatFraction();
		if ( strength <= 0f )
		{
			fire.SetBurningState( false );
			return;
		}

		if ( fire.TimeUntilNextDamageTick <= 0f )
		{
			DealFireDamage( fire, strength );
			SpreadHeat( fire, dt, strength );
			fire.TimeUntilNextDamageTick = fire.DamageInterval;
		}
	}

	static void DealFireDamage( FireComponent fire, float strength )
	{
		if ( !fire.DealDamage ) return;

		var radius = fire.GetDamageRadius();
		if ( radius <= 0f ) return;

		var damage = (fire.BaseDamagePerSecond + fire.BaseDamagePerSecond * strength * fire.DamageScaleByHeat) * fire.DamageInterval;
		if ( damage <= 0f ) return;

		var scene = Game.ActiveScene;
		if ( !scene.IsValid() ) return;

		// HL2 uses FIRE_SPREAD_DAMAGE_MULTIPLIER = 2.0:
		// outward ignition radius is 2x the visual fire radius so spread is noticeable.
		// Self-damage (via heat model) uses the base radius; outward damage uses the doubled one.
		var spreadRadius = radius * 2f;

		var hits = scene.FindInPhysics( new Sphere( fire.WorldPosition, spreadRadius ) );
		var tags = new TagSet();
		tags.Add( DamageTags.Burn );

		foreach ( var damageable in hits.SelectMany( x => x.GetComponentsInParent<Component.IDamageable>() ).Distinct() )
		{
			if ( damageable is not Component target )
				continue;

			if ( target.GameObject == fire.GameObject )
				continue;

			if ( !fire.DamagePlayers && target.GetComponentInParent<Player>( true ).IsValid() )
				continue;

			if ( fire.RequireLineOfSight )
			{
				var tr = scene.Trace.Ray( fire.WorldPosition, target.WorldPosition )
					.IgnoreGameObjectHierarchy( fire.GameObject )
					.WithTag( "map" )
					.WithoutTags( "trigger" )
					.Run();

				if ( tr.Hit && tr.GameObject.IsValid() && !target.GameObject.Root.IsDescendant( tr.GameObject ) )
					continue;
			}

			var info = new DamageInfo( damage, fire.GameObject )
			{
				Origin = fire.WorldPosition,
				Position = target.WorldPosition,
				Tags = tags
			};

			damageable.Damage( info );
		}
	}

	static void SpreadHeat( FireComponent fire, float dt, float strength )
	{
		if ( fire.SpreadHeatScale <= 0f ) return;

		var scene = Game.ActiveScene;
		if ( !scene.IsValid() ) return;

		var radius = fire.GetDamageRadius();
		if ( radius <= 0f ) return;

		var nearbyFires = scene.FindInPhysics( new Sphere( fire.WorldPosition, radius ) )
			.SelectMany( x => x.GetComponentsInParent<FireComponent>() )
			.Where( x => x.IsValid() && x != fire && x.Enabled )
			.Distinct()
			.ToArray();

		if ( nearbyFires.Length == 0 ) return;

		var outputHeat = strength * fire.HeatLevel * fire.SpreadHeatScale * dt;
		if ( outputHeat <= 0f ) return;

		var perFireHeat = outputHeat / nearbyFires.Length;
		foreach ( var nearbyFire in nearbyFires )
			AddHeat( nearbyFire, perFireHeat, false );
	}
}

/// <summary>
/// Fire component. Attached directly to the burning GameObject by <see cref="FireSystem.Ignite"/>.
/// Owns heat state, damage ticking, and a reference to the separate visual GO (particles + sound).
/// Because this component lives on the burning object, it is automatically destroyed when that
/// object is destroyed — no orphaned damage dealers or invisible fire possible.
/// </summary>
public sealed class FireComponent : Component
{
	[Property, Group( "Fire" )] public bool Enabled { get; set; } = true;
	[Property, Group( "Fire" )] public bool StartLit { get; set; } = false;
	[Property, Group( "Fire" )] public bool InfiniteFuel { get; set; } = true;
	[Property, Group( "Fire" )] public float FuelSeconds { get; set; } = 10f;

	[Property, Group( "Heat" )] public float MaxHeat { get; set; } = 100f;
	[Property, Group( "Heat" )] public float AttackTime { get; set; } = 4f;
	[Property, Group( "Heat" )] public float GrowthRate { get; set; } = 1f;
	[Property, Group( "Heat" )] public float InitialHeatAbsorb { get; set; } = 8f;
	[Property, Group( "Heat" )] public float AbsorbRate { get; set; } = 1f;
	[Property, Group( "Heat" )] public float IncomingHeatScale { get; set; } = 0.25f;
	[Property, Group( "Heat" )] public float ExtinguishAbsorbScale { get; set; } = 0.75f;
	[Property, Group( "Heat" )] public float MaxHeatAbsorb { get; set; } = 64f;

	[Property, Group( "Damage" )] public bool DealDamage { get; set; } = true;
	[Property, Group( "Damage" )] public bool DamagePlayers { get; set; } = true;
	[Property, Group( "Damage" )] public bool RequireLineOfSight { get; set; } = true;
	[Property, Group( "Damage" )] public float BaseDamagePerSecond { get; set; } = 8f;
	[Property, Group( "Damage" )] public float DamageScaleByHeat { get; set; } = 1f;
	[Property, Group( "Damage" )] public float DamageInterval { get; set; } = 0.2f;
	[Property, Group( "Damage" )] public float FireSize { get; set; } = 64f;
	[Property, Group( "Damage" )] public float MinimumDamageRadius { get; set; } = 16f;

	[Property, Group( "Spread" )] public float SpreadHeatScale { get; set; } = 0.2f;


	[Sync] public float HeatLevel { get; internal set; } = 0f;
	[Sync] public bool IsBurning { get; private set; } = false;

	internal float CurrentHeatAbsorb { get; set; }
	internal float RemainingFuel { get; set; }
	internal TimeUntil TimeUntilNextDamageTick { get; set; }

	/// <summary>
	/// The separate root-level GO that holds fire particles and sound.
	/// Lives independently so BecomeOrphan/TemporaryEffect can fade it out on extinguish.
	/// Its position is synced to this component's WorldPosition every frame.
	/// </summary>
	internal GameObject _igniteInstance;

	protected override void OnStart()
	{
		CurrentHeatAbsorb = InitialHeatAbsorb;
		RemainingFuel = FuelSeconds;
		TimeUntilNextDamageTick = 0f;

		if ( StartLit )
		{
			HeatLevel = MaxHeat;
			SetBurningState( true );
		}
	}

	protected override void OnUpdate()
	{
		// Keep the visual GO co-located with this (the burning object).
		// It lives at root level for BecomeOrphan particle fade, so sync manually.
		if ( _igniteInstance.IsValid() )
			_igniteInstance.WorldPosition = WorldPosition;

		FireSystem.Update( this, Time.Delta );
	}

	protected override void OnDisabled()
	{
		SetBurningState( false );
	}

	protected override void OnDestroy()
	{
		// The burning object was destroyed — shut down the visual GO.
		// Disabling emitters lets BecomeOrphan/WaitForChildEffects fade particles naturally.
		ShutdownIgniteInstance();
		IsBurning = false;
	}

	public void AddHeat( float heat ) => FireSystem.AddHeat( this, heat, false );
	public void AddSelfHeat( float heat ) => FireSystem.AddHeat( this, heat, true );
	public void Extinguish( float heat ) => FireSystem.DoExtinguish( this, heat );

	public float GetHeatFraction()
	{
		if ( MaxHeat <= 0f ) return 0f;
		return Math.Clamp( HeatLevel / MaxHeat, 0f, 1f );
	}

	public float GetDamageRadius()
	{
		var strength = GetHeatFraction();
		var radius = FireSize * 0.5f * strength;
		return Math.Max( MinimumDamageRadius, radius );
	}

	internal void SetBurningState( bool burning )
	{
		if ( IsBurning == burning ) return;
		IsBurning = burning;

		if ( !burning )
			ShutdownIgniteInstance();
	}

	void ShutdownIgniteInstance()
	{
		if ( !_igniteInstance.IsValid() ) return;

		// Stop all emitters so particles and sound fade out naturally.
		// TemporaryEffect (BecomeOrphan=true, WaitForChildEffects=true) destroys the GO
		// once every ParticleEffect empties.
		foreach ( var emitter in _igniteInstance.GetComponentsInChildren<ParticleModelEmitter>( true ) )
			emitter.Enabled = false;

		// Stop the looping sound immediately
		foreach ( var sound in _igniteInstance.GetComponentsInChildren<SoundBoxComponent>( true ) )
			sound.Enabled = false;

		_igniteInstance = null;
	}
}

/// <summary>
/// Thin wrapper component for map-placed fires. Calls FireSystem.Ignite() on start,
/// which attaches FireComponent to this GameObject and spawns the visual ignite prefab.
/// </summary>
public sealed class EnvFire : Component
{
	protected override void OnStart()
	{
		FireSystem.Ignite( GameObject );
	}
}
