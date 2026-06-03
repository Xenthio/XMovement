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
	[Property] public float ReleaseThrownTime { get; set; } = 0.57f;
	[Property] public float GrabInsertTime { get; set; } = 0.73f;
	[Property] public float InsertCompleteTime { get; set; } = 1.20f;

	private GameObject _thrownMagazine;
	private GameObject _insertMagazine;
	private SkinnedModelRenderer _playerRenderer;
	private SkinnedModelRenderer _gunRenderer;
	private Player _player;
	private Vector3 _lastHandPosition;
	private float _lastHandUpdateTime;

	// hard coded rotation, bit annoying to setup.
	private Rotation HeldRotation = Rotation.From( 0, 90, 0 );

	protected override void OnAwake()
	{
		_gunRenderer = GetComponentInChildren<SkinnedModelRenderer>();
	}

	protected override void OnUpdate()
	{
		// Track hand position while holding thrown magazine for velocity calculation
		if ( _thrownMagazine.IsValid() && _playerRenderer.IsValid() )
		{
			if ( _playerRenderer.GetBoneObject( HandBoneName ) is GameObject boneObject )
			{
				_lastHandPosition = boneObject.WorldPosition;
				_lastHandUpdateTime = Time.Now;
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
			_lastHandUpdateTime = Time.Now;

			Log.Info( $"Spawning thrown magazine {_thrownMagazine}" );
		}
	}

	private void ReleaseThrownMagazine()
	{
		if ( !_thrownMagazine.IsValid() ) return;
		if ( !_playerRenderer.IsValid() ) return;

		var pos = _thrownMagazine.WorldPosition;
		var rot = _thrownMagazine.WorldRotation;

		// get velocity from last frame
		Vector3 handVelocity = Vector3.Zero;
		if ( _playerRenderer.GetBoneObject( HandBoneName ) is GameObject boneObject )
		{
			var currentHandPosition = boneObject.WorldPosition;
			var deltaTime = Time.Now - _lastHandUpdateTime;

			if ( deltaTime > 0 )
			{
				handVelocity = (currentHandPosition - _lastHandPosition) / deltaTime;
			}
		}

		_thrownMagazine.Parent = null;
		_thrownMagazine.WorldPosition = pos;
		_thrownMagazine.WorldRotation = rot;
		 
		if ( _thrownMagazine.GetComponentInChildren<Rigidbody>() is { } rb )
		{
			rb.MotionEnabled = true;

			// velocity plus some
			var throwDir = GameObject.WorldTransform.Rotation.Backward;
			rb.Velocity = handVelocity + (throwDir * 250f + Vector3.Down * 80f);
			rb.AngularVelocity = Vector3.Random * 10f;
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
