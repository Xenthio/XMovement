using Sandbox;
namespace XMovement;

public partial class PlayerWalkControllerComplex : Component
{
	[Property, Group( "Camera" ), Change( "SetupCamera" )]
	public CameraModes CameraMode { get; set; } = CameraModes.ThirdPerson;
	public enum CameraModes
	{
		FirstPerson,
		ThirdPerson,
		Manual,
	}

	[Property, Group( "Camera" ), ShowIf( "CameraMode", CameraModes.Manual )]
	public CameraComponent Camera { get; set; }


	[Property, Group( "Camera" ), ShowIf( "CameraMode", CameraModes.FirstPerson ), Change( "SetupCamera" )]
	public bool PlayerShadowsOnly { get; set; } = true;


	[Property, Group( "Camera" ), ShowIf( "CameraMode", CameraModes.FirstPerson ), Change( "SetupCamera" )]
	public Vector3 FirstPersonOffset { get; set; } = new Vector3( 0, 0, 0 );


	[Property, Group( "Camera" ), ShowIf( "CameraMode", CameraModes.ThirdPerson ), Change( "SetupCamera" )]
	public Vector3 ThirdPersonOffset { get; set; } = new Vector3( -180, 0, 0 );

	[Property, InputAction, Group( "Camera" )]
	public string CameraToggleAction { get; set; } = "View";

	public virtual void OnCameraModeChanged() { }
	public void SetupCamera()
	{
		OnCameraModeChanged();
		if ( CameraMode != CameraModes.Manual && !Camera.IsValid() )
		{
			var cameraobj = Scene.CreateObject();
			cameraobj.SetParent( Head );
			cameraobj.Name = "Camera";
			Camera = cameraobj.AddComponent<CameraComponent>();
			Camera.Enabled = false;
			Camera.TargetEye = StereoTargetEye.Both;
		}
		if ( CameraMode == CameraModes.FirstPerson )
		{
			Camera.LocalPosition = FirstPersonOffset;
		}
		if ( CameraMode == CameraModes.ThirdPerson )
		{
			Camera.LocalPosition = ThirdPersonOffset;
		}
		if ( !IsProxy && Game.IsPlaying )
		{
			Camera.Enabled = true;
		}
		UpdateBodyVisibility();
	}

	public void UpdateCamera()
	{
		if ( CameraMode == CameraModes.ThirdPerson )
		{
			var fraction = 1f;
			var start = Head.WorldPosition;
			var end = Head.WorldPosition + (ThirdPersonOffset * Head.WorldRotation);
			var tr = Scene.Trace.Ray( start, end ).IgnoreDynamic().Run();
			fraction = tr.Fraction;
			Camera.LocalPosition = ThirdPersonOffset * fraction;
		}
		if ( Input.Pressed( CameraToggleAction ) )
		{
			if ( CameraMode == CameraModes.ThirdPerson ) CameraMode = CameraModes.FirstPerson;
			else if ( CameraMode == CameraModes.FirstPerson ) CameraMode = CameraModes.ThirdPerson;
		}
	}

	public void UpdateBodyVisibility()
	{
		if ( IsProxy )
		{
			foreach ( var mdlrenderer in Body.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndChildren ) )
			{
				mdlrenderer.RenderType = Sandbox.ModelRenderer.ShadowRenderType.On;
			}
			return;
		}
		if ( CameraMode == CameraModes.FirstPerson )
		{
			foreach ( var mdlrenderer in Body.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndChildren ) )
			{
				mdlrenderer.RenderType = PlayerShadowsOnly ? Sandbox.ModelRenderer.ShadowRenderType.ShadowsOnly : Sandbox.ModelRenderer.ShadowRenderType.On;
			}
		}
		if ( CameraMode == CameraModes.ThirdPerson )
		{
			if ( BodyModelRenderer.RenderType == Sandbox.ModelRenderer.ShadowRenderType.ShadowsOnly && PlayerShadowsOnly )
			{
				foreach ( var mdlrenderer in Body.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndChildren ) )
				{
					mdlrenderer.RenderType = Sandbox.ModelRenderer.ShadowRenderType.On;
				}
			}
		}
	}
}
