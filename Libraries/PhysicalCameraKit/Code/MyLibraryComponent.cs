using System;
using Sandbox;

/// <summary>
/// A physical camera profile describes the optical and image characteristics of
/// a real camera without making assumptions about the game that owns it.
/// </summary>
public sealed class PhysicalCameraProfile
{
	public string Name { get; set; }
	/// <summary>35mm-equivalent focal length used to match the advertised framing.</summary>
	public float FocalLengthMm { get; set; }
	/// <summary>Actual lens focal length used for depth-of-field calculations.</summary>
	public float PhysicalFocalLengthMm { get; set; }
	public float SensorWidthMm { get; set; } = 8.5f;
	public float SensorHeightMm { get; set; } = 6.4f;
	public float Aperture { get; set; } = 1.78f;
	public float MinimumFocusDistanceMm { get; set; } = 100.0f;
	public float FocusDistance { get; set; } = 3.0f;
	public float LensDistortion { get; set; }
	public float ExposureCompensation { get; set; }
	public Vector3 MountOffsetMm { get; set; }
	public bool HardwareMeasurementsEstimated { get; set; } = true;

	public float FieldOfView => PhysicalCameraMath.HorizontalFieldOfView( FocalLengthMm, 36.0f );
	public float EntrancePupilDiameterMm => PhysicalFocalLengthMm / MathF.Max( Aperture, 0.01f );
	public float SensorDiagonalMm => MathF.Sqrt( SensorWidthMm * SensorWidthMm + SensorHeightMm * SensorHeightMm );
	public Vector3 MountOffset => MountOffsetMm / PhysicalCameraMath.MillimetersPerSboxUnit;

	public PhysicalCameraProfile Clone()
	{
		return new PhysicalCameraProfile
		{
			Name = Name,
			FocalLengthMm = FocalLengthMm,
			PhysicalFocalLengthMm = PhysicalFocalLengthMm,
			SensorWidthMm = SensorWidthMm,
			SensorHeightMm = SensorHeightMm,
			Aperture = Aperture,
			MinimumFocusDistanceMm = MinimumFocusDistanceMm,
			FocusDistance = FocusDistance,
			LensDistortion = LensDistortion,
			ExposureCompensation = ExposureCompensation,
			MountOffsetMm = MountOffsetMm,
			HardwareMeasurementsEstimated = HardwareMeasurementsEstimated
		};
	}
}

/// <summary>Unit-safe optical calculations used by physical camera profiles.</summary>
public static class PhysicalCameraMath
{
	public const float MillimetersPerSboxUnit = 25.4f;

	public static float HorizontalFieldOfView( float focalLengthMm, float sensorWidthMm )
	{
		return 2.0f * MathF.Atan( sensorWidthMm / (2.0f * MathF.Max( focalLengthMm, 0.01f )) ) * (180.0f / MathF.PI);
	}

	public static float MillimetersToSboxUnits( float millimeters ) => millimeters / MillimetersPerSboxUnit;

	public static float SboxUnitsToMillimeters( float units ) => units * MillimetersPerSboxUnit;

	public static float CircleOfConfusionMm( float sensorWidthMm, float sensorHeightMm )
	{
		return MathF.Sqrt( sensorWidthMm * sensorWidthMm + sensorHeightMm * sensorHeightMm ) / 1500.0f;
	}

	public static float HyperfocalDistanceMm( float focalLengthMm, float aperture, float circleOfConfusionMm )
	{
		var focalLength = MathF.Max( focalLengthMm, 0.01f );
		return focalLength * focalLength / (MathF.Max( aperture, 0.01f ) * MathF.Max( circleOfConfusionMm, 0.0001f )) + focalLength;
	}

	public static void DepthOfFieldLimitsMm( float focalLengthMm, float aperture, float circleOfConfusionMm, float focusDistanceMm, out float nearMm, out float farMm )
	{
		var focalLength = MathF.Max( focalLengthMm, 0.01f );
		var focusDistance = MathF.Max( focusDistanceMm, focalLength + 0.01f );
		var hyperfocal = HyperfocalDistanceMm( focalLength, aperture, circleOfConfusionMm );
		nearMm = hyperfocal * focusDistance / (hyperfocal + focusDistance - focalLength);
		var farDenominator = hyperfocal - focusDistance + focalLength;
		farMm = farDenominator <= 0.0f ? float.PositiveInfinity : hyperfocal * focusDistance / farDenominator;
	}

	public static float ExposureValue100( float aperture, float shutterSeconds )
	{
		return MathF.Log2( MathF.Max( aperture * aperture / MathF.Max( shutterSeconds, 0.000001f ), 0.000001f ) );
	}

	public static float ExposureValue( float aperture, float shutterSeconds, float iso )
	{
		return ExposureValue100( aperture, shutterSeconds ) - MathF.Log2( MathF.Max( iso, 1.0f ) / 100.0f );
	}
}

public enum PhysicalTonemapping
{
	None,
	Aces,
	AgX
}

public enum PhysicalStabilizationMode
{
	Off,
	Optical,
	Cinematic,
	Action
}

/// <summary>Image settings emitted by a physical camera each frame.</summary>
public sealed class PhysicalCameraImageSettings
{
	public float FStop { get; init; }
	public float EquivalentFocalLengthMm { get; init; }
	public float PhysicalFocalLengthMm { get; init; }
	public float EntrancePupilDiameterMm { get; init; }
	public float FocusDistance { get; init; }
	public float NearDepthOfField { get; init; }
	public float FarDepthOfField { get; init; }
	public float DepthOfFieldStrength { get; init; }
	public float LensDistortion { get; init; }
	public float Exposure { get; init; }
	public float ExposureValue100 { get; init; }
	public float ExposureValue { get; init; }
	public PhysicalTonemapping Tonemapping { get; init; }
}

/// <summary>
/// A small, game-agnostic camera rig for physical-camera style rendering.
/// Attach it to the same GameObject as a CameraComponent and provide a profile
/// through code or a game-specific wrapper.
/// </summary>
[Title( "Physical Camera" )]
[Category( "PhysicalCameraKit" )]
public sealed class PhysicalCameraComponent : Component, ICameraModifier
{
	public enum Lens
	{
		UltraWide,
		Main,
		Telephoto
	}

	public static readonly PhysicalCameraProfile IPhone17ProMaxUltraWide = new()
	{
		Name = "iPhone 17 Pro Max - 13mm",
		FocalLengthMm = 13.0f,
		PhysicalFocalLengthMm = 2.74f,
		SensorWidthMm = 7.6f,
		SensorHeightMm = 5.7f,
		Aperture = 2.2f,
		MinimumFocusDistanceMm = 20.0f,
		LensDistortion = -0.08f,
		MountOffsetMm = new Vector3( 0.0f, 18.29f, 0.0f )
	};

	public static readonly PhysicalCameraProfile IPhone17ProMaxMain = new()
	{
		Name = "iPhone 17 Pro Max - 24mm",
		FocalLengthMm = 24.0f,
		PhysicalFocalLengthMm = 6.53f,
		SensorWidthMm = 9.8f,
		SensorHeightMm = 7.35f,
		Aperture = 1.78f,
		MinimumFocusDistanceMm = 120.0f,
		LensDistortion = -0.015f,
		MountOffsetMm = Vector3.Zero
	};

	public static readonly PhysicalCameraProfile IPhone17ProMaxTelephoto = new()
	{
		Name = "iPhone 17 Pro Max - 100mm",
		FocalLengthMm = 100.0f,
		PhysicalFocalLengthMm = 15.56f,
		SensorWidthMm = 5.6f,
		SensorHeightMm = 4.2f,
		Aperture = 2.8f,
		MinimumFocusDistanceMm = 800.0f,
		LensDistortion = 0.01f,
		MountOffsetMm = new Vector3( 0.0f, -15.75f, 0.0f )
	};

	[Property] public Lens ActiveLens { get; set; } = Lens.Main;
	[Property] public float Zoom { get; set; } = 1.0f;
	[Property] public bool AutoSelectLens { get; set; } = true;
	[Property] public float LensBlendSpeed { get; set; } = 8.0f;
	[Property] public bool AutoFocus { get; set; } = true;
	[Property] public GameObject FocusTarget { get; set; }
	[Property] public float FocusDistance { get; set; } = 200.0f;
	[Property] public float FocusTraceDistance { get; set; } = 4096.0f;
	[Property] public float FocusSpeed { get; set; } = 8.0f;
	[Property] public float FocusDeadZone { get; set; } = 2.0f;
	[Property] public float FocusAcquisitionDelay { get; set; } = 0.08f;
	[Property] public float FocusHuntAmount { get; set; } = 0.0025f;
	[Property] public bool AutoExposure { get; set; } = true;
	[Property] public float ISO { get; set; } = 100.0f;
	[Property] public float ShutterSpeedSeconds { get; set; } = 1.0f / 120.0f;
	[Property] public float ReferenceExposureValue { get; set; } = 12.0f;
	[Property] public float PhysicalExposureInfluence { get; set; } = 0.25f;
	[Property] public float ExposureSpeed { get; set; } = 3.0f;
	[Property] public float MinimumExposure { get; set; } = 1.0f;
	[Property] public float MaximumExposure { get; set; } = 3.0f;
	[Property] public float DepthOfFieldStrength { get; set; } = 1.0f;
	[Property] public float DepthOfFieldBlurSize { get; set; } = 30.0f;
	[Property] public bool UsePhysicalBlurSize { get; set; } = true;
	[Property] public float DepthOfFieldFocusRange { get; set; } = 500.0f;
	[Property] public bool DepthOfFieldFrontBlur { get; set; } = false;
	[Property] public bool DepthOfFieldBackBlur { get; set; } = true;
	[Property] public PhysicalTonemapping Tonemapping { get; set; } = PhysicalTonemapping.AgX;
	[Property] public bool EnableSensorEffects { get; set; } = true;
	[Property] public float SensorEffectStrength { get; set; } = 1.0f;
	[Property] public PhysicalStabilizationMode Stabilization { get; set; } = PhysicalStabilizationMode.Optical;
	[Property] public bool EnableHandheldMotion { get; set; } = true;
	[Property] public float HandheldPositionAmount { get; set; } = 0.012f;
	[Property] public float HandheldRotationAmount { get; set; } = 0.45f;
	[Property] public float HandheldFrequency { get; set; } = 1.7f;
	[Property] public float WalkBobPositionAmount { get; set; } = 0.08f;
	[Property] public float WalkBobRotationAmount { get; set; } = 1.2f;
	[Property] public float WalkBobFrequency { get; set; } = 1.8f;
	[Property] public float WalkBobReferenceSpeed { get; set; } = 140.0f;
	[Property] public float AccelerationLagAmount { get; set; } = 0.00002f;
	[Property] public float MaximumAccelerationLag { get; set; } = 0.12f;
	[Property] public float RecordingFrameRate { get; set; } = 30.0f;
	[Property] public CameraComponent Camera { get; set; }
	[Property] public Sandbox.Tonemapping TonemappingComponent { get; set; }
	[Property] public Sandbox.DepthOfField DepthOfFieldComponent { get; set; }
	[Property] public Sandbox.ChromaticAberration ChromaticAberrationComponent { get; set; }
	[Property] public Sandbox.FilmGrain FilmGrainComponent { get; set; }
	[Property] public Sandbox.Vignette VignetteComponent { get; set; }
	[Property] public Sandbox.MotionBlur MotionBlurComponent { get; set; }

	public PhysicalCameraProfile CurrentProfile { get; private set; }
	public float CurrentExposure { get; private set; }
	public PhysicalCameraImageSettings CurrentImageSettings { get; private set; }
	public event Action<PhysicalCameraImageSettings> SettingsUpdated;

	float _blend;
	float _time;
	float _focusDistance;
	Lens _blendedLens;
	float _blendedNativeZoom;
	float _focusAcquisitionTimer;
	float _lastFocusTargetDistance;
	Vector3 _lastViewPosition;
	Vector3 _lastViewVelocity;
	float _walkBobPhase;
	bool _hasLastViewPosition;

	protected override void OnStart()
	{
		CurrentProfile = GetProfile( ActiveLens ).Clone();
		CurrentExposure = CurrentProfile.ExposureCompensation;
		_focusDistance = FocusDistance;
		_blendedLens = ActiveLens;
		_blendedNativeZoom = GetNativeZoom( ActiveLens );
		_lastFocusTargetDistance = FocusDistance;
	}

	protected override void OnUpdate()
	{
		var camera = ResolveCamera();
		if ( camera is null ) return;
		_time += Time.Delta;
		if ( AutoSelectLens ) ActiveLens = SelectLensForZoom( Zoom );
		if ( ActiveLens != _blendedLens )
		{
			_blendedLens = ActiveLens;
			_blend = 0.0f;
		}
		var target = GetProfile( ActiveLens );
		_blend = Math.Clamp( _blend + Time.Delta * LensBlendSpeed, 0.0f, 1.0f );
		CurrentProfile = LerpProfile( CurrentProfile, target, _blend );
		_blendedNativeZoom = MathX.Lerp( _blendedNativeZoom, GetNativeZoom( ActiveLens ), _blend );
		var targetFocusDistance = FocusDistance;
		if ( AutoFocus && FocusTarget.IsValid() )
			targetFocusDistance = camera.WorldPosition.Distance( FocusTarget.WorldPosition );
		else if ( AutoFocus )
			targetFocusDistance = TraceFocusDistance( camera );
		var minimumFocusDistance = PhysicalCameraMath.MillimetersToSboxUnits( CurrentProfile.MinimumFocusDistanceMm );
		targetFocusDistance = MathF.Max( targetFocusDistance, minimumFocusDistance );
		if ( MathF.Abs( targetFocusDistance - _lastFocusTargetDistance ) > FocusDeadZone )
		{
			_focusAcquisitionTimer = FocusAcquisitionDelay;
			_lastFocusTargetDistance = targetFocusDistance;
		}
		_focusAcquisitionTimer = MathF.Max( _focusAcquisitionTimer - Time.Delta, 0.0f );
		if ( _focusAcquisitionTimer <= 0.0f )
		{
			var focusError = targetFocusDistance - _focusDistance;
			var hunt = MathF.Sin( _time * 5.3f ) * MathF.Abs( focusError ) * FocusHuntAmount;
			_focusDistance = MathX.Lerp( _focusDistance, targetFocusDistance + hunt, Math.Clamp( Time.Delta * FocusSpeed, 0.0f, 1.0f ) );
		}
		CurrentProfile.FocusDistance = _focusDistance;
		var exposureValue100 = PhysicalCameraMath.ExposureValue100( CurrentProfile.Aperture, ShutterSpeedSeconds );
		var exposureValue = PhysicalCameraMath.ExposureValue( CurrentProfile.Aperture, ShutterSpeedSeconds, ISO );
		var physicalExposureCompensation = (ReferenceExposureValue - exposureValue) * PhysicalExposureInfluence;
		CurrentExposure = Math.Clamp( CurrentProfile.ExposureCompensation + physicalExposureCompensation, -5.0f, 5.0f );
		var focusDistanceMm = PhysicalCameraMath.SboxUnitsToMillimeters( _focusDistance );
		var circleOfConfusionMm = PhysicalCameraMath.CircleOfConfusionMm( CurrentProfile.SensorWidthMm, CurrentProfile.SensorHeightMm );
		PhysicalCameraMath.DepthOfFieldLimitsMm( CurrentProfile.PhysicalFocalLengthMm, CurrentProfile.Aperture, circleOfConfusionMm, focusDistanceMm, out var nearDofMm, out var farDofMm );
		CurrentImageSettings = new PhysicalCameraImageSettings
		{
			FStop = CurrentProfile.Aperture,
			EquivalentFocalLengthMm = CurrentProfile.FocalLengthMm,
			PhysicalFocalLengthMm = CurrentProfile.PhysicalFocalLengthMm,
			EntrancePupilDiameterMm = CurrentProfile.EntrancePupilDiameterMm,
			FocusDistance = CurrentProfile.FocusDistance,
			NearDepthOfField = PhysicalCameraMath.MillimetersToSboxUnits( nearDofMm ),
			FarDepthOfField = float.IsPositiveInfinity( farDofMm ) ? float.PositiveInfinity : PhysicalCameraMath.MillimetersToSboxUnits( farDofMm ),
			DepthOfFieldStrength = DepthOfFieldStrength,
			LensDistortion = CurrentProfile.LensDistortion,
			Exposure = CurrentExposure,
			ExposureValue100 = exposureValue100,
			ExposureValue = exposureValue,
			Tonemapping = Tonemapping
		};
		ApplyTonemapping( camera );
		ApplyDepthOfField( camera );
		ApplySensorEffects( camera );
		SettingsUpdated?.Invoke( CurrentImageSettings );
	}

	public int CameraOrder => 300;

	public void ModifyCamera( CameraComponent camera, ref CameraView view )
	{
		if ( !camera.IsValid() || camera != ResolveCamera() ) return;

		var frameDelta = MathF.Max( Time.Delta, 0.0001f );
		var movementDelta = _hasLastViewPosition ? view.Position - _lastViewPosition : Vector3.Zero;
		var viewVelocity = movementDelta / frameDelta;
		var viewAcceleration = _hasLastViewPosition ? (viewVelocity - _lastViewVelocity) / frameDelta : Vector3.Zero;
		var movementSpeed = movementDelta.WithZ( 0.0f ).Length / frameDelta;
		var walkAmount = Math.Clamp( movementSpeed / MathF.Max( WalkBobReferenceSpeed, 0.01f ), 0.0f, 1.0f );
		_lastViewPosition = view.Position;
		_lastViewVelocity = viewVelocity;
		_hasLastViewPosition = true;
		var opticalZoom = MathF.Max( Zoom / MathF.Max( _blendedNativeZoom, 0.01f ), 1.0f );
		view.FieldOfView = CurrentProfile.FieldOfView / opticalZoom;
		var mountOffset = view.Rotation * CurrentProfile.MountOffset;
		view.Position += mountOffset;

		if ( !EnableHandheldMotion )
			return;

		var handheldScale = Stabilization switch
		{
			PhysicalStabilizationMode.Optical => 0.65f,
			PhysicalStabilizationMode.Cinematic => 0.3f,
			PhysicalStabilizationMode.Action => 0.15f,
			_ => 1.0f
		};
		var walkScale = Stabilization switch
		{
			PhysicalStabilizationMode.Optical => 0.8f,
			PhysicalStabilizationMode.Cinematic => 0.45f,
			PhysicalStabilizationMode.Action => 0.25f,
			_ => 1.0f
		};

		_walkBobPhase += frameDelta * WalkBobFrequency * ( 0.25f + walkAmount * 0.75f );
		var idlePositionWave = new Vector3(
			MathF.Sin( _time * HandheldFrequency * 1.31f ),
			MathF.Sin( _time * HandheldFrequency * 0.87f + 1.2f ),
			MathF.Sin( _time * HandheldFrequency * 1.73f + 2.4f ) ) * HandheldPositionAmount * handheldScale;
		var walkPositionWave = new Vector3(
			MathF.Sin( _walkBobPhase * 2.0f ) * 0.25f,
			MathF.Sin( _walkBobPhase ) * 0.15f,
			MathF.Abs( MathF.Sin( _walkBobPhase ) ) ) * WalkBobPositionAmount * walkAmount * walkScale;
		var localAcceleration = view.Rotation.Inverse * viewAcceleration;
		var accelerationLag = -localAcceleration * AccelerationLagAmount * handheldScale;
		if ( accelerationLag.Length > MaximumAccelerationLag )
			accelerationLag = accelerationLag.Normal * MaximumAccelerationLag;
		var positionWave = idlePositionWave + walkPositionWave + accelerationLag;
		var idleRotationWave = new Angles(
			MathF.Sin( _time * HandheldFrequency * 1.11f ) * HandheldRotationAmount,
			MathF.Sin( _time * HandheldFrequency * 0.79f + 0.8f ) * HandheldRotationAmount,
			MathF.Sin( _time * HandheldFrequency * 1.43f + 1.6f ) * HandheldRotationAmount ) * handheldScale;
		var walkRotationWave = new Angles(
			MathF.Sin( _walkBobPhase * 2.0f ) * WalkBobRotationAmount * 0.35f,
			MathF.Sin( _walkBobPhase ) * WalkBobRotationAmount * 0.2f,
			MathF.Sin( _walkBobPhase ) * WalkBobRotationAmount * 0.15f ) * walkAmount * walkScale;
		var rotationWave = idleRotationWave + walkRotationWave;

		view.Position += view.Rotation * positionWave;
		view.Rotation = (view.Rotation.Angles() + rotationWave).ToRotation();
	}

	public PhysicalCameraProfile GetProfile( Lens lens )
	{
		return lens switch
		{
			Lens.UltraWide => IPhone17ProMaxUltraWide,
			Lens.Telephoto => IPhone17ProMaxTelephoto,
			_ => IPhone17ProMaxMain
		};
	}

	CameraComponent ResolveCamera()
	{
		if ( Camera.IsValid() ) return Camera;

		Camera = GameObject.Components.Get<CameraComponent>( FindMode.EverythingInSelfAndChildren );
		return Camera;
	}

	float TraceFocusDistance( CameraComponent camera )
	{
		var end = camera.WorldPosition + camera.WorldRotation.Forward * FocusTraceDistance;
		var trace = Scene.Trace.Ray( camera.WorldPosition, end )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "trigger", "player" ) 
			.Run();

		return trace.Hit ? camera.WorldPosition.Distance( trace.HitPosition ) : FocusDistance;
	}

	void ApplyTonemapping( CameraComponent camera )
	{
		camera.EnablePostProcessing = true;
		if ( !TonemappingComponent.IsValid() )
			TonemappingComponent = camera.GameObject.Components.Get<Tonemapping>() ?? camera.GameObject.AddComponent<Tonemapping>();

		TonemappingComponent.Mode = Tonemapping switch
		{
			PhysicalTonemapping.Aces => Sandbox.Tonemapping.TonemappingMode.ACES,
			PhysicalTonemapping.AgX => Sandbox.Tonemapping.TonemappingMode.AgX,
			_ => Sandbox.Tonemapping.TonemappingMode.Linear
		};
		TonemappingComponent.Enabled = Tonemapping != PhysicalTonemapping.None;
		TonemappingComponent.AutoExposureEnabled = AutoExposure;
		TonemappingComponent.MinimumExposure = MinimumExposure;
		TonemappingComponent.MaximumExposure = MaximumExposure;
		TonemappingComponent.ExposureCompensation = CurrentExposure;
		TonemappingComponent.Rate = Math.Clamp( ExposureSpeed, 1.0f, 10.0f );
	}

	void ApplyDepthOfField( CameraComponent camera )
	{
		camera.EnablePostProcessing = true;
		if ( !DepthOfFieldComponent.IsValid() )
			DepthOfFieldComponent = camera.GameObject.Components.Get<DepthOfField>() ?? camera.GameObject.AddComponent<DepthOfField>();

		DepthOfFieldComponent.Enabled = DepthOfFieldStrength > 0.0f;
		DepthOfFieldComponent.FocalDistance = MathF.Max( _focusDistance, 1.0f );
		var physicalFocusRange = GetPhysicalFocusRange();
		DepthOfFieldComponent.FocusRange = UsePhysicalBlurSize ? physicalFocusRange : MathF.Max( DepthOfFieldFocusRange, 0.0f );
		var relativePupilSize = CurrentProfile.EntrancePupilDiameterMm / MathF.Max( CurrentProfile.SensorDiagonalMm, 0.01f );
		var apertureBlur = relativePupilSize / 0.30f;
		var blurSize = UsePhysicalBlurSize ? DepthOfFieldBlurSize * apertureBlur : DepthOfFieldBlurSize;
		DepthOfFieldComponent.BlurSize = Math.Clamp( DepthOfFieldStrength * blurSize, 0.0f, 100.0f );
		DepthOfFieldComponent.FrontBlur = DepthOfFieldFrontBlur;
		DepthOfFieldComponent.BackBlur = DepthOfFieldBackBlur;
	}

	void ApplySensorEffects( CameraComponent camera )
	{
		if ( !ChromaticAberrationComponent.IsValid() )
			ChromaticAberrationComponent = camera.GameObject.Components.Get<ChromaticAberration>() ?? camera.GameObject.AddComponent<ChromaticAberration>();
		if ( !FilmGrainComponent.IsValid() )
			FilmGrainComponent = camera.GameObject.Components.Get<FilmGrain>() ?? camera.GameObject.AddComponent<FilmGrain>();
		if ( !VignetteComponent.IsValid() )
			VignetteComponent = camera.GameObject.Components.Get<Vignette>() ?? camera.GameObject.AddComponent<Vignette>();
		if ( !MotionBlurComponent.IsValid() )
			MotionBlurComponent = camera.GameObject.Components.Get<MotionBlur>() ?? camera.GameObject.AddComponent<MotionBlur>();

		ChromaticAberrationComponent.Enabled = EnableSensorEffects;
		FilmGrainComponent.Enabled = EnableSensorEffects;
		VignetteComponent.Enabled = EnableSensorEffects;
		MotionBlurComponent.Enabled = EnableSensorEffects;
		if ( !EnableSensorEffects ) return;

		var strength = Math.Clamp( SensorEffectStrength, 0.0f, 2.0f );
		var edgeStress = Math.Clamp( 24.0f / MathF.Max( CurrentProfile.FocalLengthMm, 1.0f ), 0.25f, 2.0f );
		ChromaticAberrationComponent.Scale = Math.Clamp( 0.025f * edgeStress * strength, 0.0f, 1.0f );
		ChromaticAberrationComponent.Offset = new Vector3( 6.0f, 2.0f, 4.0f );

		var isoNoise = MathF.Sqrt( MathF.Max( ISO, 25.0f ) / 100.0f ) - 0.5f;
		FilmGrainComponent.Intensity = Math.Clamp( isoNoise * 0.035f * strength, 0.0f, 0.35f );
		FilmGrainComponent.Response = 0.65f;

		VignetteComponent.Intensity = Math.Clamp( 0.08f * edgeStress * strength, 0.0f, 0.4f );
		VignetteComponent.Smoothness = 0.75f;
		VignetteComponent.Roundness = 0.85f;

		var frameExposureFraction = ShutterSpeedSeconds * MathF.Max( RecordingFrameRate, 1.0f );
		MotionBlurComponent.Scale = Math.Clamp( frameExposureFraction * 0.08f * strength, 0.0f, 0.25f );
	}

	float GetPhysicalFocusRange()
	{
		var focusDistanceMm = PhysicalCameraMath.SboxUnitsToMillimeters( _focusDistance );
		var circleOfConfusionMm = PhysicalCameraMath.CircleOfConfusionMm( CurrentProfile.SensorWidthMm, CurrentProfile.SensorHeightMm );
		PhysicalCameraMath.DepthOfFieldLimitsMm( CurrentProfile.PhysicalFocalLengthMm, CurrentProfile.Aperture, circleOfConfusionMm, focusDistanceMm, out var nearMm, out var farMm );
		var nearRange = MathF.Max( focusDistanceMm - nearMm, 0.0f );
		var farRange = float.IsPositiveInfinity( farMm ) ? PhysicalCameraMath.SboxUnitsToMillimeters( FocusTraceDistance ) : MathF.Max( farMm - focusDistanceMm, 0.0f );
		return Math.Clamp( PhysicalCameraMath.MillimetersToSboxUnits( MathF.Min( nearRange, farRange ) ), 0.0f, 1000.0f );
	}

	public static Lens SelectLensForZoom( float zoom )
	{
		if ( zoom < 0.9f ) return Lens.UltraWide;
		if ( zoom >= 4.0f ) return Lens.Telephoto;
		return Lens.Main;
	}

	static float GetNativeZoom( Lens lens )
	{
		return lens switch
		{
			Lens.UltraWide => 0.5f,
			Lens.Telephoto => 4.0f,
			_ => 1.0f
		};
	}

	static PhysicalCameraProfile LerpProfile( PhysicalCameraProfile from, PhysicalCameraProfile to, float amount )
	{
		return new PhysicalCameraProfile
		{
			Name = to.Name,
			FocalLengthMm = MathX.Lerp( from.FocalLengthMm, to.FocalLengthMm, amount ),
			PhysicalFocalLengthMm = MathX.Lerp( from.PhysicalFocalLengthMm, to.PhysicalFocalLengthMm, amount ),
			SensorWidthMm = MathX.Lerp( from.SensorWidthMm, to.SensorWidthMm, amount ),
			SensorHeightMm = MathX.Lerp( from.SensorHeightMm, to.SensorHeightMm, amount ),
			Aperture = MathX.Lerp( from.Aperture, to.Aperture, amount ),
			MinimumFocusDistanceMm = MathX.Lerp( from.MinimumFocusDistanceMm, to.MinimumFocusDistanceMm, amount ),
			FocusDistance = MathX.Lerp( from.FocusDistance, to.FocusDistance, amount ),
			LensDistortion = MathX.Lerp( from.LensDistortion, to.LensDistortion, amount ),
			ExposureCompensation = MathX.Lerp( from.ExposureCompensation, to.ExposureCompensation, amount ),
			MountOffsetMm = Vector3.Lerp( from.MountOffsetMm, to.MountOffsetMm, amount ),
			HardwareMeasurementsEstimated = from.HardwareMeasurementsEstimated || to.HardwareMeasurementsEstimated
		};
	}
}
