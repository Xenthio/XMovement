using Sandbox;
using XMovement;

public sealed class OneWayTeleport : Component, Component.ITriggerListener
{
	[Property] public GameObject Destination {  get; set; }
	public void OnTriggerEnter( Collider other )
	{
		if ( other.IsProxy ) return;

		PlayerMovement playerMovement = other.GameObject.Components.GetInParentOrSelf<PlayerMovement>();
		if ( playerMovement != null )
		{
			// A player hit the respawn trigger!
			playerMovement.WorldPosition = Destination.WorldPosition;
		}
	}
}
