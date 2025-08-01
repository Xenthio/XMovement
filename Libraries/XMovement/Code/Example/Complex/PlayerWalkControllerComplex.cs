﻿using Sandbox;
namespace XMovement;

public partial class PlayerWalkControllerComplex : Component
{
	[RequireComponent] public PlayerMovement Controller { get; set; }

	[Button( "Setup Template/Apply Changes" )]
	public void SetupTemplate()
	{
		SetupBody();
		SetupHead();
		SetupCamera();
		SetupVR();
	}

	protected override void OnStart()
	{
		base.OnStart();
		if ( !IsProxy )
		{
			SetupBody();
			SetupHead();
			SetupCamera();
			SetupVR();
		}
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( !Game.IsPlaying ) return;

		if ( !UseSceneCamera )
			Camera.Enabled = !IsProxy;

		if ( !IsProxy )
			UpdateCamera();

		DoEyeLook();

		if ( !IsProxy )
		{
			BuildFrameInput();
			DoUsing();
			if ( Controller.MovementFrequency == PlayerMovement.MovementFrequencyMode.PerUpdate ) DoMovement();
		}
		Animate();
	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();
		if ( !Game.IsPlaying ) return;

		if ( !IsProxy )
		{
			UpdateCrouching();
			if ( Controller.MovementFrequency == PlayerMovement.MovementFrequencyMode.PerFixedUpdate ) DoMovement();
		}
		Animate();

		// HACK: For shitty networking purposes do this per FixedUpdate, we cant do this on start because new players wont ever fucking run that on join, nor any other function.
		UpdateBodyVisibility();
	}
}
