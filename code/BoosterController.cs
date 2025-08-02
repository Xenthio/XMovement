using Sandbox;

public sealed class BoosterController : Component, Component.ITriggerListener
{
	private const string SOUND_PATH = "sound/sfx/booster.sound";
	Rigidbody rb;
	[Property] public float Force = 1000;

	Vector3 finalVelocity;
	public void OnTriggerEnter( Collider other )
	{
		rb = other.Components.Get<Rigidbody>();
		if ( rb != null && rb.IsValid && rb.Network.IsOwner )
		{
			finalVelocity = Force * GameObject.WorldRotation.Forward;
			rb.Velocity = finalVelocity;

			if ( SoundEffectPlayer.Singleton != null )
			{
				SoundEffectPlayer.Singleton.PlaySoundAtPosition( SOUND_PATH, rb.WorldPosition, rb.Network.Owner.Id );
			}

			//MarbleController.Local?.SuppressLateralForceForDuration( 1.3f );
		}
	}

	public void OnTriggerExit( Collider other )
	{
		// Log.Info( other );
	}
}
