using System.Threading;

/// <summary>
/// Manages magazine animations during reload. Supports both:
/// - Manual timing-based triggers (for now, hardcoded per weapon)
/// - Animation events (future, when animation files support them)
/// 
/// Example SMG hodltype reload timings (1.2s total):
/// - 0.23s: Grab thrown magazine
/// - 0.57s: Toss thrown magazine
/// - 0.73s: Grab new magazine from pouch
/// - 1.20s: Insert magazine, delete insert mag, show gun mag
/// </summary>
public class MagazineAnimator : Component
{
	[Property] public GameObject ThrownMagazinePrefab { get; set; }
	[Property] public GameObject InsertMagazinePrefab { get; set; }
	[Property] public string HandBoneName { get; set; } = "hold_l";

	/// Reload animation timings (in seconds), these defaults are for the smg hold type
	[Property] public float GrabThrownTime { get; set; } = 0.23f;
	[Property] public float ReleaseThrownTime { get; set; } = 0.50f;
	[Property] public float GrabInsertTime { get; set; } = 0.73f;
	[Property] public float InsertCompleteTime { get; set; } = 1.20f;

	private GameObject _thrownMagazine;
	private GameObject _insertMagazine;
	private SkinnedModelRenderer _playerRenderer;
	private SkinnedModelRenderer _gunRenderer;
	private Player _player;
	private Vector3 _lastHandPosition;
	private Vector3 _handVelocity;

	// hard coded rotation, bit annoying to setup.
	private Rotation HeldRotation = Rotation.From( 0, 90, 0 );

	protected override void OnAwake()
	{
		_gunRenderer = GetComponentInChildren<SkinnedModelRenderer>();
	}

	private void UpdateRenderMode()
	{
		// copy shadow rendering mode from gun
		if ( _gunRenderer.IsValid() )
		{
			if ( _thrownMagazine.IsValid() && _thrownMagazine.GetComponent<ModelRenderer>() is ModelRenderer thrownRenderer )
			{ 
				thrownRenderer.RenderType = _gunRenderer.RenderType;
			}
			if ( _insertMagazine.IsValid() && _insertMagazine.GetComponent<ModelRenderer>() is ModelRenderer insertRenderer )
			{
				insertRenderer.RenderType = _gunRenderer.RenderType;
			}
		}
	}

	protected override void OnUpdate()
	{
		// copy shadow rendering mode from gun
		UpdateRenderMode();

		// Track hand velocity while holding thrown magazine
		if ( _thrownMagazine.IsValid() && _playerRenderer.IsValid() )
		{
			if ( _playerRenderer.GetBoneObject( HandBoneName ) is GameObject boneObject )
			{
				var currentHandPosition = boneObject.WorldPosition;

				// Calculate velocity using Time.Delta (frame time)
				if ( Time.Delta > 0 )
				{
					_handVelocity = (currentHandPosition - _lastHandPosition) / Time.Delta;
				}

				_lastHandPosition = currentHandPosition;
			}
		}
	}

	/// <summary>
	/// Call this from ViewModel.OnReloadStart() to drive magazine animations via timing.
	/// Fires at hardcoded frame timings (configurable via properties).
	/// </summary>
	public async void PlayReloadAnimation( CancellationToken ct = default )
	{
		_player = GameObject.Root.GetComponent<Player>();
		if ( _player.IsValid() )
		{
			_playerRenderer = _player.Body.GetComponent<SkinnedModelRenderer>(); 
		}
		Log.Info( $"Starting magazine animation with player {_player} and gun renderer {_gunRenderer}" );
		try
		{
			// Frame 7: Grab thrown magazine
			await GameTask.DelaySeconds( GrabThrownTime, ct );
			if ( ct.IsCancellationRequested ) return;
			OnGrabThrownMagazine();

			// Frame 17: Toss thrown magazine
			await GameTask.DelaySeconds( ReleaseThrownTime - GrabThrownTime, ct );
			if ( ct.IsCancellationRequested ) return;
			OnReleaseThrownMagazine();

			// Frame 22: Grab new magazine from pouch
			await GameTask.DelaySeconds( GrabInsertTime - ReleaseThrownTime, ct );
			if ( ct.IsCancellationRequested ) return;
			OnGrabInsertMagazine();

			// Frame 36: Insert magazine, complete reload
			await GameTask.DelaySeconds( InsertCompleteTime - GrabInsertTime, ct );
			if ( ct.IsCancellationRequested ) return;
			OnInsertComplete();
		}
		catch ( OperationCanceledException )
		{
			// Reload was cancelled, clean up
			CleanupMagazines();
		}
	}

	/// <summary>
	/// Grab and parent the thrown magazine to hand bone.
	/// Called at grab frame timing.
	/// </summary>
	public void OnGrabThrownMagazine()
	{ 
		SpawnThrownMagazine();
	}

	/// <summary>
	/// Unparent thrown magazine and give it toss velocity.
	/// Called at release frame timing.
	/// </summary>
	public void OnReleaseThrownMagazine()
	{
		ReleaseThrownMagazine();
	}

	/// <summary>
	/// Grab and parent the insert magazine to hand bone.
	/// Called at grab frame timing.
	/// </summary>
	public void OnGrabInsertMagazine()
	{
		SpawnInsertMagazine();
	}

	/// <summary>
	/// Insert magazine complete: delete insert mag and show gun magazine.
	/// Called at insert frame timing.
	/// </summary>
	public void OnInsertComplete()
	{
		DeleteInsertMagazine();
		ShowGunMagazine();
	}

	private void SpawnThrownMagazine()
	{
		if ( !ThrownMagazinePrefab.IsValid() ) return;
		if ( !_playerRenderer.IsValid() ) return;

		HideGunMagazine();

		if ( _playerRenderer.GetBoneObject( HandBoneName ) is GameObject boneObject )
		{
			_thrownMagazine = ThrownMagazinePrefab.Clone( new CloneConfig
			{ 
				StartEnabled = true
			} );

			_thrownMagazine.Parent = boneObject;
			 
			_thrownMagazine.LocalRotation = HeldRotation;

			//disable physics while held
			if ( _thrownMagazine.GetComponentInChildren<Rigidbody>() is { } rb )
			{
				rb.MotionEnabled = false;
			}

			//velocity calc
			_lastHandPosition = boneObject.WorldPosition;
			_handVelocity = Vector3.Zero;
			// copy shadow rendering mode from gun
			UpdateRenderMode();

			Log.Info( $"Spawning thrown magazine {_thrownMagazine}" );
		}
	}

	private void ReleaseThrownMagazine()
	{
		if ( !_thrownMagazine.IsValid() ) return;

		var pos = _thrownMagazine.WorldPosition;
		var rot = _thrownMagazine.WorldRotation;

		_thrownMagazine.Parent = null;
		_thrownMagazine.WorldPosition = pos;
		_thrownMagazine.WorldRotation = rot;

		if ( _thrownMagazine.GetComponentInChildren<Rigidbody>() is { } rb )
		{
			rb.MotionEnabled = true;

			// Use the pre-calculated hand velocity from OnUpdate() plus additional throw force
			var throwDir = GameObject.WorldTransform.Rotation.Backward;
			rb.Velocity = _handVelocity; // + (throwDir * 25f + Vector3.Down * 0f);
			rb.AngularVelocity = Vector3.Random * 10f;

			Log.Info( $"Releasing magazine with hand velocity: {_handVelocity}" );
		}

		if ( _thrownMagazine.GetComponentInChildren<ModelRenderer>() is ModelRenderer thrownRenderer )
		{
			// we want it to be visible in first person after being thrown
			thrownRenderer.RenderType = ModelRenderer.ShadowRenderType.On;
		}
		_thrownMagazine = null;
	}

	private void SpawnInsertMagazine()
	{
		if ( !InsertMagazinePrefab.IsValid() ) return;
		if ( !_playerRenderer.IsValid() ) return;

		if ( _playerRenderer.GetBoneObject( HandBoneName ) is GameObject boneObject )
		{
			_insertMagazine = InsertMagazinePrefab.Clone();

			_insertMagazine.Parent = boneObject;

			_insertMagazine.LocalRotation = HeldRotation;
			// copy shadow rendering mode from gun
			UpdateRenderMode();

			if ( _insertMagazine.GetComponentInChildren<Rigidbody>() is { } rb )
			{
				rb.Velocity = Vector3.Zero;
				rb.AngularVelocity = Vector3.Zero;
				rb.MotionEnabled = false;
			}
		}
	}

	private void DeleteInsertMagazine()
	{
		if ( _insertMagazine.IsValid() )
			_insertMagazine.Destroy();

		_insertMagazine = null;
	}

	private void HideGunMagazine()
	{
		if ( _gunRenderer.IsValid() )
			_gunRenderer.SetBodyGroup( "magazine", 1 ); 
	}

	private void ShowGunMagazine()
	{
		if ( _gunRenderer.IsValid() )
			_gunRenderer.SetBodyGroup( "magazine", 0 );
	}

	private void CleanupMagazines()
	{
		if ( _thrownMagazine.IsValid() ) _thrownMagazine.Destroy();
		if ( _insertMagazine.IsValid() ) _insertMagazine.Destroy();
		ShowGunMagazine();
	}
}
