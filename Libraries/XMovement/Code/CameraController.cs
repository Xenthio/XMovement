using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using XMovement;
namespace XMovement;
public sealed class CameraController : Component
{
	[Property] public float VerticalOffset = 16f;
	[Property] public Vector2 AutocamFollowSpeed = new( 80f, 1200f );
	[Property] public Vector2 SlerpRateMinMax = new( 0.5f, 3f );
	[Property] public Vector2 ZoomHeightOffset = new Vector2( 16f, 64f );
	[Property] public GameObject Head { get; set; }
	private Vector3 targetPosition;

	public class CameraOverrideZone
	{
		public int Priority;
		public float Yaw;
		public float Pitch;
		public float Distance;
		public bool AllowYaw;
		public CameraOverrideZone( int priority, float yaw, float pitch, float distance, bool allowYaw = false )
		{
			Priority = priority;
			Yaw = yaw;
			Pitch = pitch;
			Distance = distance;
			AllowYaw = allowYaw;
		}
	}
	private Dictionary<int, CameraOverrideZone> registeredCameraOverrides = new();
	private CameraOverrideZone activeCameraOverride = null;

	private float distance;
	[Property] public float ZoomLevel { get; set; } = 500f;
	[Property] public float MinDistance { get; set; } = 250.0f;
	[Property] public float MaxDistance { get; set; } = 750.0f;
	[Property] public float DistanceStep { get; set; } = 60.0f;

	private Angles eyeAngles { get; set; }

	private float cameraLockoutTime = 0f;
	private bool hasInitWithLocal = false;

	private Transform? virtualTransform;
	private Transform transitionStartTransform;
	private float transitionDuration;
	private float transitionProgress;

	public static CameraController Local
	{
		get
		{
			if ( !_local.IsValid() )
			{
				_local = Game.ActiveScene.GetAllComponents<CameraController>().FirstOrDefault( x => x.Network.IsOwner );
			}
			return _local;
		}
	}
	private static CameraController _local = null;


	private GameObject CameraObject;
	protected override void OnAwake()
	{
		base.OnAwake();

		if ( !GameObject.IsValid || Network.IsProxy )
		{
			return; // This isn't ours!
		}

		CameraComponent camera = Scene.GetAllComponents<CameraComponent>().Where( x => x.IsMainCamera ).FirstOrDefault();
		if (camera == null)
		{
			//create camera!
			Log.Info( "Create camera!" );
			var cameraobj = Scene.CreateObject();
			cameraobj.Name = "MainCamera";
			camera = cameraobj.AddComponent<CameraComponent>();
			camera.Enabled = true;
			CameraObject = cameraobj;
		} else
		{
			CameraObject = camera.GameObject;
		}
		ResetAngles();
	}

	protected override void OnPreRender()
	{
		try
		{
			if ( !GameObject.IsValid || IsProxy || Network.IsProxy )
				return;

			UpdateCamera();
		}
		catch ( Exception e )
		{
			Log.Error( e.StackTrace );
		}
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if ( Network.IsProxy )
		{
			return; // This isn't ours!
		}

		if ( virtualTransform.HasValue )
		{
			// Overriding time
			HandleVirtual( MathF.Min( Time.Delta, 0.05f ) );
		}
		else if ( LookDirectionController.Local != null && LookDirectionController.Local.IsValid )
		{
			if (!hasInitWithLocal)
			{
				ResetAngles();
			}

			HandleManualLook();

			if ( activeCameraOverride == null )
			{
				HandleMovement( MathF.Min( Time.Delta, 0.05f ) );
			}
			else
			{
				// We have an override!
				HandleOverride( MathF.Min( Time.Delta, 0.05f ), activeCameraOverride.AllowYaw && cameraLockoutTime <= 0);
				if ( activeCameraOverride.AllowYaw && cameraLockoutTime > 0 )
				{
					HandleMovement( MathF.Min( Time.Delta, 0.05f ) );
				}
			}

			LookDirectionController.Local.WorldRotation = eyeAngles.ToRotation();
		}
	}

	const float DEFAULT_YAW_SPEED = 2.70f;
	const float DEFAULT_PITCH_SPEED = 1.60f;
	void HandleManualLook()
	{
		// Handle rotating the LookDirection object
		var e = eyeAngles;
		Angles inputAngles = Input.AnalogLook;

		if ( Input.UsingController )
		{
			if ( MathF.Abs( inputAngles.pitch / DEFAULT_PITCH_SPEED ) < 0.2f )
			{
				inputAngles.pitch = 0;
			}
			else
			{
				inputAngles.pitch *= 0.415f;
			}

			if ( MathF.Abs( inputAngles.yaw / DEFAULT_YAW_SPEED ) < 0.2f )
			{
				inputAngles.yaw = 0;
			}
			else
			{
				inputAngles.yaw *= 0.55f;
			}
		}

		e += inputAngles;
		e.pitch = e.pitch.Clamp( -45, 80 );
		e.roll = 0;
		eyeAngles = e;

		if ( MathF.Abs( inputAngles.yaw ) > 0.015f )
		{
			cameraLockoutTime = 1.5f;
		}
	}

	Vector3 currentPosition, previousPosition, currentVelocity, previousVelocity;
	Rotation? targetRotation;
	Rotation currentRotation, slerpedRotation;
	float slerpRate;
	Vector3 delta;
	void HandleMovement( float dt )
	{
		currentRotation = eyeAngles.WithPitch( 0 ).WithRoll( 0 ).ToRotation();

		if ( cameraLockoutTime <= 0 )
		{
			currentPosition = LookDirectionController.Local.WorldPosition.WithZ( 0 );
			delta = (currentPosition - previousPosition);

			if (delta.LengthSquared > 1000000)
			{
				// we moved more than 1000 units. wipe all previous info.
				previousPosition = LookDirectionController.Local.WorldPosition.WithZ( 0 );
				currentPosition = previousPosition;
				previousVelocity = Vector3.Zero;
				currentVelocity = Vector3.Zero;
			} else
			{
				currentVelocity = Vector3.Lerp( previousVelocity, (currentPosition - previousPosition) / dt, dt * 12 );
				previousVelocity = currentVelocity;

				if ( currentVelocity.Length > AutocamFollowSpeed.x )
				{
					targetRotation = Rotation.FromToRotation( Vector3.Forward, currentVelocity.WithZ( 0 ).Normal );
				}
				else
				{
					targetRotation = null;
				}
			}

			if ( targetRotation.HasValue )
			{
				slerpRate = MathX.Lerp( dt * SlerpRateMinMax.x, dt * SlerpRateMinMax.y, (currentVelocity.Length - AutocamFollowSpeed.x) / (AutocamFollowSpeed.y - AutocamFollowSpeed.x) );
				slerpedRotation = Rotation.Slerp( currentRotation, targetRotation.Value, slerpRate, true );
				eyeAngles = eyeAngles.WithYaw( slerpedRotation.Yaw() );
			}

			previousPosition = currentPosition;
		}
		else
		{
			cameraLockoutTime -= Time.Delta;
			if ( cameraLockoutTime <= 0 )
			{
				previousPosition = LookDirectionController.Local.WorldPosition.WithZ( 0 );
				currentPosition = previousPosition;
				previousVelocity = Vector3.Zero;
				currentVelocity = Vector3.Zero;
			}
		}
	}

	void HandleOverride( float dt, bool suppressRotation )
	{
		distance = distance.LerpTo( activeCameraOverride.Distance, dt * 5 );

		if ( suppressRotation )
		{
			currentRotation = eyeAngles.ToRotation();
			targetRotation = new Angles( activeCameraOverride.Pitch > -90 ? activeCameraOverride.Pitch : currentRotation.Pitch(), activeCameraOverride.Yaw <= -999 ? eyeAngles.yaw : activeCameraOverride.Yaw, 0 ).ToRotation();

			slerpedRotation = Rotation.Slerp( currentRotation, targetRotation.Value, dt * 5 );
			eyeAngles = slerpedRotation.Angles();

			previousPosition = LookDirectionController.Local.WorldPosition.WithZ( 0 );
			currentPosition = previousPosition;
		}
	}

	void HandleVirtual( float dt )
	{
		// are we still transitioning to this override?
		if ( transitionProgress < transitionDuration )
		{
			transitionProgress += dt;

			if ( CameraObject.IsValid )
			{
				CameraObject.WorldPosition = Vector3.Lerp( transitionStartTransform.Position, virtualTransform.Value.Position, transitionProgress / transitionDuration );
				CameraObject.WorldRotation = Rotation.Slerp( transitionStartTransform.Rotation, virtualTransform.Value.Rotation, transitionProgress / transitionDuration );
			}
		}
		else
		{
			// I guess we just sorta hang out here at this new destination
		}
	}

	bool wasDead = false;
	void UpdateCamera()
	{
		//var CameraObject = Scene.GetAllComponents<CameraComponent>().Where( x => x.IsMainCamera ).FirstOrDefault();

		if ( CameraObject is null ) return;

		//CameraObject = Scene.Components.Get<CameraComponent>( FindMode.InDescendants ).GameObject;
		if ( CameraObject.IsValid() )
		{
			if ( PlayerWalkControllerSimple.Local != null && !virtualTransform.HasValue )
			{
				if ( activeCameraOverride == null || activeCameraOverride.Distance <= 0 )
				{
					ZoomLevel += -Input.MouseWheel.y * RealTime.Delta * 1000.0f * DistanceStep;
					ZoomLevel = ZoomLevel.Clamp( MinDistance, MaxDistance );
					distance = distance.LerpTo( ZoomLevel, 30f * RealTime.Delta, true );
				}

				var distanceA = distance.LerpInverse( MinDistance, MaxDistance );

				/*if ( !MarbleController.Local.IsDead )
				{
					targetPosition = Vector3.Lerp( targetPosition, LookDirectionController.Local.WorldPosition + (VerticalOffset * Vector3.Up), wasDead ? 1.0f : 30.0f * RealTime.Delta );
				}*/

				targetPosition = LookDirectionController.Local.WorldPosition + (VerticalOffset * Vector3.Up);

				var playerRotation = LookDirectionController.Local.WorldRotation;

				var height = ZoomHeightOffset.x.LerpTo( ZoomHeightOffset.y, distanceA );
				var center = targetPosition + Vector3.Up * height + playerRotation.Backward * 8.0f;
				var targetPos = center + playerRotation.Backward * ZoomLevel;

				var tr = Scene.PhysicsWorld.Trace.Ray( center, targetPos )
					.WithoutTags( "trigger", "player", "nocamera" )
					.Radius( 8.0f )
					.Run();

				if ( tr.Hit && false ) // it's being weird in some circumstances. just don't bother.
				{
					distance = Math.Min( distance, tr.Distance );
				}
				distanceA = distance.LerpInverse( MinDistance, MaxDistance );
				height = ZoomHeightOffset.x.LerpTo( ZoomHeightOffset.y, distanceA );
				center = targetPosition + Vector3.Up * height + playerRotation.Backward * 8.0f;

				CameraObject.WorldPosition = center + playerRotation.Backward * distance;
				CameraObject.WorldRotation = playerRotation;
				CameraObject.WorldRotation *= Rotation.FromPitch( distanceA * 10.0f );

				wasDead = false;
			}
			else
			{
				// We're dead! Camera locks in place until we respawn
				wasDead = true;
			}
		}
	}

	public void ResetAngles()
	{
		var e = eyeAngles;
		e.pitch = 10;
		e.yaw = 0;
		e.roll = 0;
		eyeAngles = e;

		if ( LookDirectionController.Local.IsValid() )
		{
			currentPosition = LookDirectionController.Local.WorldPosition.WithZ( 0 );
			previousPosition = currentPosition;
			previousVelocity = Vector3.Zero;
			currentVelocity = Vector3.Zero;

			cameraLockoutTime = 1f;
			hasInitWithLocal = true;
		}

		registeredCameraOverrides.Clear();
		EvaluateCameraOverrides();
	}

	public void Teleport( Vector3 newPos, float newYaw )
	{
		var e = eyeAngles;
		e.pitch = 10;
		e.yaw = newYaw;
		e.roll = 0;

		if ( LookDirectionController.Local.IsValid() )
		{
			currentPosition = newPos.WithZ( 0 );
			previousPosition = currentPosition;
			previousVelocity = Vector3.Zero;
			currentVelocity = Vector3.Zero;

			cameraLockoutTime = 1f;
		}

		eyeAngles = e;
	}

	public void EvaluateCameraOverrides()
	{
		int highestPriority = -1;
		CameraOverrideZone newZone = null;
		foreach ( CameraOverrideZone z in registeredCameraOverrides.Values )
		{
			if ( z.Priority > highestPriority )
			{
				newZone = z;
				highestPriority = z.Priority;
			}
		}

		if ( highestPriority >= 0 )
		{
			// We found one!
			activeCameraOverride = newZone;
		}
		else
		{
			activeCameraOverride = null;
		}
	}

	public void RegisterCameraOverride( int id, int priority, float yaw, float pitch, float distance, bool allowYaw )
	{
		if ( !registeredCameraOverrides.ContainsKey( id ) )
		{
			if (allowYaw)
			{
				cameraLockoutTime = 0f;
			}

			registeredCameraOverrides.Add( id, new CameraOverrideZone
				(
					priority, yaw, pitch, distance, allowYaw
				)
			);

			EvaluateCameraOverrides();
		}
	}

	public void UnregisterCameraOverride( int id )
	{
		registeredCameraOverrides.Remove( id );
		EvaluateCameraOverrides();
	}

	public void TransitionToVirtualTransform( Vector3 vPos, Rotation vRot, float duration )
	{
		virtualTransform = new Transform( vPos, vRot, 1 );
		transitionStartTransform = new Transform( CameraObject.WorldPosition, CameraObject.WorldRotation, 1 );
		transitionProgress = 0f;
		transitionDuration = duration;
	}

	public void ClearVirtualTransform()
	{
		virtualTransform = null;
	}
}
