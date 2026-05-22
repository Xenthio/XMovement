using XMovement;

/// <summary>
/// Bridges XMovement's jump/land callbacks into the IPlayerEvents system.
/// Lives outside the XMovement library so the library stays dependency-free.
/// </summary>
public sealed partial class Player
{
	float _preLandZ;

	protected override void OnStart()
	{
		base.OnStart();

		// Hook into XMovement's jump broadcast
		WalkController.OnJumped += OnWalkControllerJumped;
		// Hook into PlayerMovement's grounded change
		Movement.OnLanded += OnMovementLanded;

		// Tag body hierarchy "player" so addon projectiles using IgnoreGameObject(root)
		// or WithoutTags("player") don't self-hit on child colliders.
		// Sandbox sets this via the prefab; we do it here in code for parity.
		if ( Body.IsValid() )
		{
			Body.Tags.Add( "player" );
			foreach ( var col in Body.GetComponentsInChildren<Collider>() )
				col.GameObject.Tags.Add( "player" );
		}
		// Also tag the XMovement PhysicsBody GO (BoxCollider + Rigidbody for physics push)
		// which lives on a separate child GameObject tagged "movement".
		if ( Movement?.PhysicsBody.IsValid() ?? false )
			Movement.PhysicsBody.Tags.Add( "player" );
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();

		if ( WalkController.IsValid() )
			WalkController.OnJumped -= OnWalkControllerJumped;
		if ( Movement.IsValid() )
			Movement.OnLanded -= OnMovementLanded;
	}

	void OnWalkControllerJumped()
	{
		Local.IPlayerEvents.PostToGameObject( GameObject, x => x.OnJump() );
		Global.IPlayerEvents.Post( x => x.OnPlayerJumped( this ) );
	}

	void OnMovementLanded( float distance, Vector3 impactVelocity )
	{
		Local.IPlayerEvents.PostToGameObject( GameObject, x => x.OnLand( distance, impactVelocity ) );
		Global.IPlayerEvents.Post( x => x.OnPlayerLanded( this, distance, impactVelocity ) );
	}
}
