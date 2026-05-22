// ================================================================
// SandboxCompat — Pickup components
//
// Ported from Sandbox game (MIT License).
// https://github.com/Facepunch/sandbox
//
// These components are placed on world items by Sandbox addons.
// Having them here means addons that spawn prefabs with these
// components won't get missing-type errors.
// ================================================================

/// <summary>
/// Base class for world pickups. Handles trigger/collision pickup logic.
/// Ported from Sandbox (MIT).
/// </summary>
public abstract class BasePickup : Component, Component.ITriggerListener, Component.ICollisionListener
{
	[RequireComponent] public Collider Collider { get; set; }
	[Property] public SoundEvent PickupSound { get; set; }

	public virtual bool CanPickup( Player player, PlayerInventory inventory ) => true;

	protected virtual bool OnPickup( Player player, PlayerInventory inventory ) => true;

	void ITriggerListener.OnTriggerEnter( GameObject other )
	{
		if ( !Networking.IsHost ) return;
		if ( GameObject.IsDestroyed ) return;
		if ( !other.Components.TryGet( out Player player ) ) return;
		if ( !player.Components.TryGet( out PlayerInventory inventory ) ) return;
		if ( !CanPickup( player, inventory ) ) return;
		if ( !OnPickup( player, inventory ) ) return;
		PlayPickupEffects( player );
		DestroyGameObject();
	}

	void ICollisionListener.OnCollisionStart( Collision collision )
	{
		if ( !Networking.IsHost ) return;
		if ( GameObject.IsDestroyed ) return;
		if ( !collision.Other.GameObject.Root.Components.TryGet( out Player player ) ) return;
		if ( !player.Components.TryGet( out PlayerInventory inventory ) ) return;
		if ( !CanPickup( player, inventory ) ) return;
		if ( !OnPickup( player, inventory ) ) return;
		PlayPickupEffects( player );
		DestroyGameObject();
	}

	[Rpc.Broadcast]
	protected void PlayPickupEffects( Player player )
	{
		if ( Application.IsDedicatedServer ) return;
		var snd = GameObject.PlaySound( PickupSound );
		if ( !snd.IsValid() ) return;
		if ( player.IsValid() && player.IsLocalPlayer )
			snd.SpacialBlend = 0;
	}
}

/// <summary>
/// Pickup that adds items directly into the player's inventory.
/// Ported from Sandbox (MIT).
/// </summary>
public sealed class InventoryPickup : BasePickup, Component.IPressable
{
	[Property, Group( "Inventory" )] public List<GameObject> Items { get; set; }

	IPressable.Tooltip? IPressable.GetTooltip( IPressable.Event e )
	{
		if ( Items == null || Items.Count == 0 ) return null;
		return new IPressable.Tooltip( "Pick up", "inventory_2",
			string.Join( ", ", Items.Select( i => (i.GetComponent<BaseCarryable>()?.DisplayName ?? i.Name).ToUpper() ) ) );
	}

	public bool Press( IPressable.Event e )
	{
		DoPickup( e.Source.GameObject );
		return true;
	}

	[Rpc.Host]
	private void DoPickup( GameObject presserObject )
	{
		if ( !presserObject.IsValid() ) return;
		var player = presserObject.Root.GetComponent<Player>();
		if ( !player.IsValid() ) return;
		if ( OnPickup( player, player.GetComponent<PlayerInventory>() ) )
		{
			PlayPickupEffects( player );
			GameObject.Destroy();
		}
	}

	protected override bool OnPickup( Player player, PlayerInventory inventory )
	{
		if ( Items == null ) return false;
		bool consumed = false;
		foreach ( var prefab in Items )
		{
			if ( inventory.Pickup( prefab ) )
				consumed = true;
		}
		return consumed;
	}
}

/// <summary>
/// Pickup that gives reserve ammo for a matching weapon.
/// Ported from Sandbox (MIT).
/// </summary>
public sealed class AmmoPickup : BasePickup
{
	[Property, Group( "Ammo" )] public AmmoResource AmmoType { get; set; }
	[Property, Group( "Ammo" )] public int AmmoAmount { get; set; }

	public override bool CanPickup( Player player, PlayerInventory inventory )
	{
		if ( AmmoType is null ) return false;
		var ammoInv = player.GetComponent<AmmoInventory>();
		return ammoInv is not null && ammoInv.GetAmmo( AmmoType ) < AmmoType.MaxReserve;
	}

	protected override bool OnPickup( Player player, PlayerInventory inventory )
	{
		if ( AmmoType is null ) return true;
		var ammoInv = player.GetComponent<AmmoInventory>();
		return ammoInv is not null && ammoInv.AddAmmo( AmmoType, AmmoAmount ) > 0;
	}
}

/// <summary>
/// Pickup that restores player health.
/// Ported from Sandbox (MIT).
/// </summary>
public sealed class HealthPickup : BasePickup
{
	[Property, Group( "Health" )] float HealthGive { get; set; } = 0;

	public override bool CanPickup( Player player, PlayerInventory inventory )
		=> !(player.Health >= player.MaxHealth && HealthGive > 0);

	protected override bool OnPickup( Player player, PlayerInventory inventory )
	{
		player.Health = (player.Health + HealthGive).Clamp( 0, player.MaxHealth );
		return true;
	}
}
