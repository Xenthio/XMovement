using Sandbox;
using Sandbox.Citizen;
using System.Linq;
namespace XMovement;

public partial class PlayerWalkControllerSimple : Component
{
	[RequireComponent] public PlayerMovement Controller { get; set; }
	[RequireComponent] public CitizenAnimationHelper AnimationHelper { get; set; }
	[Property] public SkinnedModelRenderer ModelRenderer { get; set; }
	[Property, Group( "Head" )] public GameObject Head { get; set; }
	[Property, Group( "Head" )] public float HeadHeight { get; set; } = 64f;

	/// <summary>
	/// How quickly does the player move by default?
	/// </summary>
	[Property, Group( "Config" )] public float RunSpeed { get; set; } = 320.0f;
	[Property, Group( "Config" )] public float WalkSpeed { get; set; } = 120.0f;
	[Property, Group( "Config" )] public float CrouchSpeed { get; set; } = 80.0f;

	/// <summary>
	/// How powerful is the player's jump?
	/// </summary>
	[Property, Group( "Config" )] public float JumpPower { get; set; } = 268.3281572999747f;
	[Property, Group( "Config" )] public float JumpBuffer { get; set; } = 0.11f;
	[Property, Group( "Config" )] public float CoyoteTime { get; set; } = 0.11f;

	[Property, Group( "Config" )] public Vector2 SlopeSpeedMultiplierDownUp { get; set; } = new Vector2( 1.2f, 0.8f );
	[Property, Group( "Config" )] public Vector2 SlopeSpeedDotProductDownUp { get; set; } = new Vector2( 0.5f, -0.5f );

	[Sync] public bool IsCrouching { get; set; }

	[Sync] public bool IsSlowWalking { get; set; }

	[Sync] public bool IsNoclipping { get; set; }

	[Property] public LookDirectionController lookDirectionController { get; set; }

	/// <summary>
	/// Do we want to jump next movement update?
	/// </summary>
	public bool WantsJump { get; set; }

	private float slopeMultiplier { get; set; } = 1f;
	private float timeLastPressedJump { get; set; } = -1f;

	protected override void OnStart()
	{
		base.OnStart();
		if (!IsProxy)
		{
			Head.LocalRotation = Rotation.Identity;
			lookDirectionController.LocalRotation = Rotation.Identity;
		}
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( !IsProxy )
		{
			DoEyeLook();
			BuildFrameInput();
			if ( Controller.MovementFrequency == PlayerMovement.MovementFrequencyMode.PerUpdate ) DoMovement();
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( !IsProxy )
		{
			if ( Controller.MovementFrequency == PlayerMovement.MovementFrequencyMode.PerFixedUpdate ) DoMovement();
		}
		Animate();
	}

	public void DoMovement()
	{
		Controller.PrepareMovement();

		BuildInput();
		BuildWishVelocity();

		if ( Time.Now - Controller.TimeLastGrounded <= CoyoteTime && WantsJump ) Jump();

		Controller.Move();

		ResetFrameInput();

		Animate();
	}

	public void Jump()
	{
		// We're jumping!
		// Clear the jump buffer
		//Log.Info((Time.Now - timeLastPressedJump).ToString() + " | " + (Time.Now - Controller.TimeLastGrounded).ToString());
		timeLastPressedJump = -1;

		Controller.Jump( JumpPower );
		BroadcastPlayerJumped();
	}

	/// <summary>
	/// A network message that lets other users that we've triggered a jump.
	/// </summary>
	[Rpc.Broadcast]
	public void BroadcastPlayerJumped()
	{
		AnimationHelper?.TriggerJump();
	}

	private void BuildFrameInput()
	{
		if ( Input.Pressed( "Jump" ) )
		{
			//WantsJump = true;
			timeLastPressedJump = Time.Now;
		} else if (!Input.Down("Jump") && Time.Now - Controller.TimeLastJumped < Controller.JumpHoldDuration && Time.Now - Controller.TimeLastJumped > 0.125f && Controller.Velocity.z > 0 )
		{
			Controller.Velocity = Controller.Velocity.WithZ( Controller.Velocity.z * 0.66f );
			Controller.TimeLastJumped = Time.Now - (Controller.JumpHoldDuration * 2);
		}

		Controller.IsHoldingJump = Input.Down( "Jump" );

		if (Time.Now - timeLastPressedJump <= JumpBuffer)
		{
			WantsJump = true;
		}
	}

	private void ResetFrameInput()
	{
		WantsJump = Time.Now - timeLastPressedJump <= JumpBuffer;
	}
	private void BuildInput()
	{
		IsSlowWalking = !Input.Down( "Run" );
		IsCrouching = Input.Down( "Duck" ) || !CanUncrouch();
	}

	protected float GetWishSpeed(Vector3 dir)
	{
		float wishSpeed = RunSpeed;
		if ( IsSlowWalking ) wishSpeed = WalkSpeed;
		if ( IsCrouching ) wishSpeed = CrouchSpeed;
		
		if (Controller.IsOnGround)
		{
			// Scale wish speed based on grounded normal (slope)
			float slopeDot = Vector3.Dot(dir, Controller.GroundNormal);

			float targetMultiplier = 1f;
			if ( slopeDot > 0 )
			{
				targetMultiplier = float.Lerp( 1f, SlopeSpeedMultiplierDownUp.x, float.Clamp(slopeDot / SlopeSpeedDotProductDownUp.x, 0f, 1f) );
			}
			else if ( slopeDot < 0 )
			{
				targetMultiplier = float.Lerp( 1f, SlopeSpeedMultiplierDownUp.y, float.Clamp( slopeDot / SlopeSpeedDotProductDownUp.y, 0f, 1f ) );
			}

			// store targetMultiplier over time
			if (targetMultiplier > slopeMultiplier)
			{
				slopeMultiplier = targetMultiplier;
			} else
			{
				slopeMultiplier = float.Lerp( slopeMultiplier, targetMultiplier, Time.Delta * 2 );
			}

			//Log.Info( slopeDot.ToString() + " | " + targetMultiplier.ToString() + "x | " + slopeMultiplier.ToString() + "x");

			wishSpeed *= slopeMultiplier;
		} else
		{
			slopeMultiplier = float.Lerp( slopeMultiplier, 1f, Time.Delta * 3 );
			wishSpeed *= slopeMultiplier;
		}

		return wishSpeed;
	}

	public void BuildWishVelocity()
	{
		//var rot = EyeAngles.WithPitch( 0f ).ToRotation();
		var rot = lookDirectionController.WorldRotation.Angles().WithPitch(0).ToRotation();

		var wishDirection = Input.AnalogMove.Normal * rot;
		wishDirection = wishDirection.WithZ( 0 );

		Controller.AnalogInput = wishDirection;
		Controller.WishVelocity = wishDirection * GetWishSpeed(wishDirection);
	}

	private bool CanUncrouch()
	{
		var b = Controller.Height;
		if ( !IsCrouching ) return true;
		Controller.Height = 72;
		var tr = Controller.TraceDirection( Vector3.Zero );
		Controller.Height = b;
		return !tr.Hit;
	}

	private float _smoothEyeHeight;
	float LastSmoothEyeHeight = 0;
	public void DoEyeLook()
	{
		if ( !IsProxy )
		{
			var eyeHeightOffset = GetEyeHeightOffset();
			_smoothEyeHeight = _smoothEyeHeight.LerpTo( eyeHeightOffset, Time.Delta * 10f );
			Controller.Height = 72 + _smoothEyeHeight;

			/*
			LocalEyeAngles += Input.AnalogLook;
			LocalEyeAngles = LocalEyeAngles.WithPitch( LocalEyeAngles.pitch.Clamp( -89f, 89f ) );*/

			// This moves our feet up when crouching in air
			var delta = _smoothEyeHeight - LastSmoothEyeHeight;
			if ( !delta.AlmostEqual( 0 ) && !Controller.IsOnGround )
			{
				var delvel = Controller.Velocity;
				if ( Controller.MovementFrequency == PlayerMovement.MovementFrequencyMode.PerUpdate ) delvel *= Time.Delta;
				delvel *= WorldScale;
				var vel = delvel - new Vector3( 0, 0, (delta * WorldScale.z) / Time.Delta );
				Controller.MoveBy( vel, true );
			}
			LastSmoothEyeHeight = _smoothEyeHeight;
		}

		if ( Head.IsValid() && lookDirectionController.IsValid() )
		{
			Head.WorldRotation = lookDirectionController.WorldRotation; //EyeAngles.ToRotation();
			Head.LocalPosition = new Vector3( 0, 0, HeadHeight + _smoothEyeHeight );
		}
	}
	protected float GetEyeHeightOffset()
	{
		if ( IsCrouching ) return -36f;
		return 0f;
	}

	public void Animate()
	{
		var rotateDifference = 0f;

		//var targetAngle = new Angles( 0, EyeAngles.yaw, 0 ).ToRotation();
		var targetAngle = ModelRenderer.WorldRotation;

		if ( !Controller.Network.IsProxy && Input.AnalogMove.LengthSquared > 0)
		{
			var rot = lookDirectionController.WorldRotation.Angles().WithPitch( 0 ).ToRotation();
			var dir = Input.AnalogMove.Normal * rot;
			targetAngle = Rotation.LookAt( dir, Vector3.Up );
		} else if (Controller.Velocity.WithZ(0).LengthSquared > 20)
		{
			targetAngle = Rotation.LookAt( Controller.Velocity.WithZ( 0 ).Normal, Vector3.Up );
		}

		var turnSpeed = 5f;
		//ModelRenderer.WorldRotation = Rotation.Slerp( ModelRenderer.WorldRotation, targetAngle, Controller.WishVelocity.Length * Time.Delta * turnSpeed );
		//ModelRenderer.WorldRotation = ModelRenderer.WorldRotation.Clamp( targetAngle, 45.0f, out var shuffle ); // lock facing to within 45 degrees of look direction
		ModelRenderer.WorldRotation = Rotation.Slerp( ModelRenderer.WorldRotation, targetAngle, Time.Delta * turnSpeed );
		//rotateDifference = shuffle;

		AnimationHelper.WithWishVelocity( Controller.WishVelocity );
		AnimationHelper.WithVelocity( Controller.Velocity );
		AnimationHelper.IsGrounded = Controller.IsOnGround;

		if ( lookDirectionController.IsValid() )
		{
			AnimationHelper.WithLook( lookDirectionController.WorldRotation.Forward * 100, 1, 1, 1.0f );
		}

		AnimationHelper.DuckLevel = IsCrouching ? 80 : 0;
	}

	public static PlayerWalkControllerSimple _local;
	public static PlayerWalkControllerSimple Local
	{
		get
		{
			if ( !_local.IsValid() )
			{
				_local = Game.ActiveScene.GetAllComponents<PlayerWalkControllerSimple>().FirstOrDefault( x => x.Network.IsOwner );
			}
			return _local;
		}
	}
}
