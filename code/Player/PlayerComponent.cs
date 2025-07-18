using XMovement;

namespace XPSBase;

public class PlayerComponent : Component, Component.IDamageable
{
	[Property, Group( "Player" )] public PlayerWalkControllerComplex WalkController;
	[Property, Group( "Player" )] public PlayerMovement Movement;
	[Property, Group( "Life" )] public float Health = 100f;

	public static PlayerComponent Local;
	protected override void OnStart()
	{
		base.OnStart();
		if ( !IsProxy )
		{
			Local = this;
		}
	}

	public void OnDamage( in DamageInfo damage )
	{
		Health -= damage.Damage;
		if ( Health <= 0 )
		{
			Health = 0;
			Log.Info( "Player has died." );
		}
	}
}
