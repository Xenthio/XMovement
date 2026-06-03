/// <summary>
/// Base class for physics-driven projectiles (rockets, grenades, bolts, etc.).
/// Attach a Rigidbody + ModelRenderer + (optional) trail/particle components alongside this.
///
/// Derived classes override <see cref="OnHit"/> to define what happens on collision.
/// Use <see cref="UpdateDirection"/> each frame/tick for guided or curved projectiles.
///
/// Ported and extended from sandbox's ProjectileEntity.
/// </summary>
public abstract class BaseProjectile : Component, Component.ICollisionListener
{
	[RequireComponent] public Rigidbody Rigidbody { get; set; }

	/// <summary>The player or NPC that fired this projectile.</summary>
	[Sync( SyncFlags.FromHost )] public Player Instigator { get; set; }

	/// <summary>The weapon that spawned this projectile (for damage attribution).</summary>
	[Sync( SyncFlags.FromHost )] public GameObject Weapon { get; set; }

	protected TimeSince TimeSinceCreated;

	protected override void OnStart()
	{
		Tags.Add( "projectile" );
	}

	protected override void OnEnabled()
	{
		TimeSinceCreated = 0;
	}

	void ICollisionListener.OnCollisionStart( Collision collision )
	{
		if ( IsProxy ) return;

		// Don't hit our own instigator immediately after firing
		var player = collision.Other.GameObject.GetComponentInParent<Player>();
		if ( Instigator.IsValid() && player.IsValid() && player == Instigator && TimeSinceCreated < 0.1f )
			return;

		OnHit( collision );
	}

	/// <summary>
	/// Called on the host when the projectile hits something.
	/// Base implementation destroys the projectile. Override to add explosions, etc.
	/// </summary>
	protected virtual void OnHit( Collision collision = default )
	{
		GameObject.Destroy();
	}

	/// <summary>
	/// Steer the projectile in a new direction at a given speed.
	/// Call this from <see cref="OnFixedUpdate"/> for guided/curved flight.
	/// </summary>
	public void UpdateDirection( Vector3 direction, float speed )
	{
		WorldRotation = Rotation.LookAt( direction, Vector3.Up );
		Rigidbody.Velocity = WorldRotation.Forward * speed;
	}
}
