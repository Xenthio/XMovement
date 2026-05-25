/// <summary>
/// Feeds incoming damage attacker positions to the DamageIndicator HUD panel.
/// Attach this to the player prefab alongside PlayerDeathEffect etc.
/// </summary>
public class PlayerDamageIndicator : Component, Local.IPlayerEvents
{
	[RequireComponent] public Player Player { get; set; }

	void Local.IPlayerEvents.OnDamage( PlayerDamageParams args )
	{
		if ( !Player.IsLocalPlayer ) return;
		if ( !args.Attacker.IsValid() ) return;

		// Find the panel — it lives somewhere in the HUD tree
		var panel = Scene.GetAllComponents<DamageIndicator>().FirstOrDefault();
		panel?.AddIndicator( args.Attacker.WorldPosition );
	}
}
