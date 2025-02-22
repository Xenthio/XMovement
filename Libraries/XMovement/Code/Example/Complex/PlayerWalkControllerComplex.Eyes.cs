using Sandbox;
namespace XMovement;

public partial class PlayerWalkControllerComplex : Component
{
	[Property, Group( "Head" )] public GameObject Head { get; set; }
	[Property, Group( "Head" )] public float HeadHeight { get; set; } = 64f;
	[Property, Group( "Config" )] public float Height { get; set; } = 72f;
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
	/// Constructs a ray using the camera's GameObject
	/// </summary>
	public virtual Ray AimRay => new( Head.WorldPosition + Camera.WorldRotation.Forward, Camera.WorldRotation.Forward );

	protected void SetupHead()
	{
		if ( !Head.IsValid() )
		{
			Head = Scene.CreateObject();
			Head.SetParent( GameObject );
			Head.Name = "Head";
			PositionHead();
		}
	}
	protected virtual void PositionHead()
	{
		if ( IsInVR ) { VRPositionHead(); return; }
		if ( Head.IsValid() )
		{
			Head.WorldRotation = EyeAngles.ToRotation();
		}
	}

	public float AimSensitivityScale = 1.0f;
	public virtual void DoEyeLook()
	{
		if ( !IsProxy )
		{
			LocalEyeAngles += Input.AnalogLook * AimSensitivityScale;
			LocalEyeAngles = LocalEyeAngles.WithPitch( LocalEyeAngles.pitch.Clamp( -89f, 89f ) );

			if ( IsInVR )
			{
				EyeAngles = Input.VR.Head.Rotation.Angles();
			}

			PositionHead();
		}
	}
}
