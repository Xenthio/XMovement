using Sandbox;
using System;
using System.Reflection.Metadata;
using XMovement;

public sealed class BouncepadController : Component, Component.ITriggerListener
{
	private const string SOUND_PATH = "sound/sfx/bouncepad.sound";

	Rigidbody rb;
	[Property] public float Force = 1000;
	[Property] public float LateralPercentageToKeep = 0f;

	Vector3 startingVelocity;
	Vector3 finalVelocity;

	float perPlayerCooldown = 0.1f;
	Dictionary<Rigidbody, float> rbCooldowns;

	protected override void OnStart()
	{
		base.OnStart();

		rbCooldowns = new();
	}

	public void OnTriggerEnter( Collider other )
	{
		rb = other.Components.Get<Rigidbody>();		
		if (rb != null && rb.IsValid && rb.Network.IsOwner )
		{
			PlayerMovement playerMovement = other.Components.GetInAncestorsOrSelf<PlayerMovement>();
			bool canLaunch = true;
			Rigidbody rbToCheck = playerMovement == null ? rb : playerMovement.PhysicsBodyRigidbody;
			if (rb.GameObject.Name.Equals("PhysicsShadow") || (rbCooldowns.TryGetValue( rbToCheck, out float v ) && Time.Now - v < perPlayerCooldown ))
			{
				canLaunch = false;
			}

			if ( canLaunch )
			{
				if ( playerMovement != null )
				{
					playerMovement.LaunchUpwards( Force );
				}
				else
				{
					startingVelocity = rb.Velocity;
					finalVelocity = Force * GameObject.WorldRotation.Up;

					// I'm being hacky here and assuming always straight up. there's dotproduct shenanigans I could do to make this better but idc right now sry
					finalVelocity = new Vector3( finalVelocity.x + (startingVelocity.x * LateralPercentageToKeep), finalVelocity.y + (startingVelocity.y * LateralPercentageToKeep), finalVelocity.z );
					rb.Velocity = finalVelocity;
				}

				if ( SoundEffectPlayer.Singleton != null )
				{
					SoundEffectPlayer.Singleton.PlaySoundAtPosition( SOUND_PATH, rb.WorldPosition, rb.Network.Owner.Id );
				}

				rbCooldowns[rb] = Time.Now;
			}
		}
	}

	public void OnTriggerExit( Collider other )
	{
		// Log.Info( other );
	}
}
