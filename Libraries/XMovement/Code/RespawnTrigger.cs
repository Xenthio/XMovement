using Sandbox;
using XMovement;

public sealed class RespawnTrigger : Component, Component.ITriggerListener
{
	public void OnTriggerEnter( Collider other )
	{
		if ( other.IsProxy ) return;

		PlayerMovement playerMovement = other.GameObject.Components.GetInParentOrSelf<PlayerMovement>();
		if (playerMovement != null)
		{
			// A player hit the respawn trigger!
			playerMovement.Respawn();
		}
	}
}
