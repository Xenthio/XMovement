using XMovement;

public class Player : Component, Component.IDamageable
{
	public static Player FindLocalPlayer() => Game.ActiveScene.GetAllComponents<Player>().Where( x => x.IsLocalPlayer ).FirstOrDefault();

	[Property] public PlayerWalkControllerComplex WalkController;
	[Property] public PlayerMovement Movement;
	[Property] public GameObject Body { get; set; }

	[Property, Range( 0, 100 ), Sync( SyncFlags.FromHost )] public float Health { get; set; } = 100;
	[Property, Range( 0, 100 ), Sync( SyncFlags.FromHost )] public float MaxHealth { get; set; } = 100;

	[Sync( SyncFlags.FromHost )] public PlayerData PlayerData { get; set; }

	public bool IsLocalPlayer => !IsProxy;
	public Guid PlayerId => PlayerData.PlayerId;
	public long SteamId => PlayerData.SteamId;
	public string DisplayName => PlayerData.DisplayName;

	protected override void OnStart()
	{
		base.OnStart();
	}

	public void OnDamage( in Sandbox.DamageInfo damage )
	{
		Health -= damage.Damage;
		if ( Health <= 0 )
		{
			Health = 0;
			Log.Info( "Player has died." );
		}
	}
}
