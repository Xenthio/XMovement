using XMovement;

/// <summary>
/// HL2-accurate drowning system.
///
/// Air depletes while the player's head is underwater (WaterLevel >= HeadSubmergedThreshold).
/// Once air runs out, drowning damage is dealt every <see cref="DamageInterval"/> seconds.
/// Air refills quickly once the head surfaces.
///
/// Plug this in as a sibling component on the Player prefab alongside
/// <see cref="PlayerFallDamage"/>. No other wiring required — it reads
/// <see cref="PlayerWalkControllerComplex.WaterLevel"/> directly.
///
/// NETWORKING:
///   Air tracking and damage decisions run on the owning client (IsProxy guard),
///   mirroring the PlayerFallDamage pattern. <see cref="Air"/> is synced from
///   host via [Sync(SyncFlags.FromHost)] so the HUD on any client can display it.
///   Damage is sent to the host via <see cref="ApplyDrownDamage"/> (Rpc.Host).
///   Sound plays on the owner via <see cref="PlayDrownSound"/> (Rpc.Owner).
///
/// NOTE: This matches the intent of <see cref="PlayerFallDamage"/> which also runs
/// on the owning client and calls Player.OnDamage directly. If the codebase later
/// adds a host-RPC damage pathway, update both components together.
/// </summary>
public class PlayerDrowning : Component, Local.IPlayerEvents
{
	[RequireComponent] public Player Player { get; set; }
	[RequireComponent] public PlayerWalkControllerComplex WalkController { get; set; }

	/// <summary>Seconds of air the player has before drowning begins. HL2 default: 15.</summary>
	[Property] public float MaxAir { get; set; } = 15f;

	/// <summary>Damage dealt per tick once air runs out. HL2 default: 10.</summary>
	[Property] public float DrownDamage { get; set; } = 10f;

	/// <summary>Seconds between each drown damage tick. HL2 default: 1.</summary>
	[Property] public float DamageInterval { get; set; } = 1f;

	/// <summary>
	/// Rate at which air refills per second when the head is above water.
	/// HL2 refills at ~3x the drain rate — full tank in ~5s after surfacing.
	/// </summary>
	[Property] public float AirRefillRate { get; set; } = 3f;

	/// <summary>
	/// WaterLevel fraction above which the player's head is considered submerged.
	/// Source uses ~0.75 for "head under" — we use the same.
	/// </summary>
	[Property, Range( 0f, 1f )] public float HeadSubmergedThreshold { get; set; } = 0.75f;

	/// <summary>
	/// Current air remaining (0–MaxAir). Synced from host so HUDs can display
	/// a breath bar on any client without extra plumbing.
	/// </summary>
	[Sync( SyncFlags.FromHost )] public float Air { get; private set; }

	private float _nextDrownTick;

	protected override void OnStart()
	{
		Air = MaxAir;
	}

	protected override void OnFixedUpdate()
	{
		// Run only on the owning client (same pattern as PlayerFallDamage).
		// The host is always the owner in listen-server or singleplayer.
		if ( IsProxy ) return;
		if ( Player.IsDead ) return;

		var headUnder = WalkController.WaterLevel >= HeadSubmergedThreshold;

		if ( headUnder )
		{
			// Drain air over time
			Air = MathF.Max( 0f, Air - Time.Delta );

			if ( Air <= 0f && Time.Now >= _nextDrownTick )
			{
				_nextDrownTick = Time.Now + DamageInterval;
				ApplyDrownDamage( DrownDamage );
				PlayDrownSound();
			}
		}
		else
		{
			// Head above water — refill at AirRefillRate u/s
			Air = MathF.Min( MaxAir, Air + Time.Delta * AirRefillRate );
		}
	}

	/// <summary>
	/// Sends the drown damage up to the host for authoritative application.
	/// Using Rpc.Host ensures the damage call reaches Player.OnDamage() which
	/// requires Networking.IsHost.
	/// </summary>
	[Rpc.Host( NetFlags.Reliable )]
	private void ApplyDrownDamage( float amount )
	{
		if ( !Networking.IsHost ) return;

		var info = new DamageInfo( amount, (GameObject)null )
		{
			Tags = new TagSet { DamageTags.Drown }
		};
		Player.OnDamage( info );
	}

	[Rpc.Owner]
	private void PlayDrownSound()
	{
		Sound.Play( "player_drown" );
	}

	void Local.IPlayerEvents.OnSpawned()
	{
		Air = MaxAir;
		_nextDrownTick = 0f;
	}
}
