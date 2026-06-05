using Sandbox;
using Sandbox.Citizen;
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

	[Sync] public bool IsCrouching { get; set; }

	[Sync] public bool IsSlowWalking { get; set; }

	[Sync] public bool IsNoclipping { get; set; }

	[Sync] public Angles LocalEyeAngles { get; set; }
	public Angles EyeAngles
	{
		get
		{
			return LocalEyeAngles + GameObject.LocalRotation.Angles();
		}
		set
		{
			LocalEyeAngles = value - GameObject.LocalRotation.Angles();
		}
	}

	/// <summary>
	/// Do we want to jump next movement update?
	/// </summary>
	public bool WantsJump { get; set; }

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

		BuildWishVelocity();
		BuildInput();

		if ( Controller.IsOnGround && WantsJump ) Jump();

		Controller.Move();

		ResetFrameInput();

		Animate();
	}

	public void Jump()
	{
		Controller.LaunchUpwards( JumpPower );
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
		if ( Input.Pressed( "Jump" ) ) WantsJump = true;
	}
	private void ResetFrameInput()
	{
		WantsJump = false;
	}
	private void BuildInput()
	{
		IsSlowWalking = Input.Down( "Run" );
		IsCrouching = Input.Down( "Duck" ) || !CanUncrouch();
	}

	protected float GetWishSpeed()
	{
		if ( Controller.IsOnGround && IsCrouching ) return CrouchSpeed;
		if ( IsSlowWalking ) return WalkSpeed;
		return RunSpeed;
	}

	public void BuildWishVelocity()
	{
		var rot = EyeAngles.WithPitch( 0f ).ToRotation();

		var wishDirection = Input.AnalogMove.Normal * rot;
		wishDirection = wishDirection.WithZ( 0 );

		Controller.WishVelocity = wishDirection * GetWishSpeed();
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

			LocalEyeAngles += Input.AnalogLook;
			LocalEyeAngles = LocalEyeAngles.WithPitch( LocalEyeAngles.pitch.Clamp( -89f, 89f ) );

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
		if ( Head.IsValid() )
		{
			Head.WorldRotation = EyeAngles.ToRotation();
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

		var targetAngle = new Angles( 0, EyeAngles.yaw, 0 ).ToRotation();

		var turnSpeed = 0.02f;
		ModelRenderer.WorldRotation = Rotation.Slerp( ModelRenderer.WorldRotation, targetAngle, Controller.WishVelocity.Length * Time.Delta * turnSpeed );
		ModelRenderer.WorldRotation = ModelRenderer.WorldRotation.Clamp( targetAngle, 45.0f, out var shuffle ); // lock facing to within 45 degrees of look direction
		rotateDifference = shuffle;

		AnimationHelper.WithWishVelocity( Controller.WishVelocity );
		AnimationHelper.WithVelocity( Controller.Velocity );
		AnimationHelper.IsGrounded = Controller.IsOnGround;
		AnimationHelper.WithLook( EyeAngles.Forward * 100, 1, 1, 1.0f );
		AnimationHelper.DuckLevel = IsCrouching ? 100 : 0;
	}
}
