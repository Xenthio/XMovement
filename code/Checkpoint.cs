using Sandbox;
using XMovement;

public sealed class Checkpoint : Component, Component.ITriggerListener
{
	[Property] public GameObject InactiveGroup;
	[Property] public GameObject ActiveGroup;

	public bool HasBeenActivated = false;

	public void OnTriggerEnter( Collider other )
	{
		if (HasBeenActivated) return;

		PlayerMovement pm = other.Components.GetInAncestorsOrSelf<PlayerMovement>();
		if (pm != null && pm.IsValid() && pm.Network.IsOwner)
		{
			pm.SetRespawn( WorldPosition + (Vector3.Up * 50), WorldRotation );
			InactiveGroup.Enabled = false;
			ActiveGroup.Enabled = true;
			HasBeenActivated = true;
		}
	}

	public void OnTriggerExit( Collider other )
	{
		// Log.Info( other );
	}
}
