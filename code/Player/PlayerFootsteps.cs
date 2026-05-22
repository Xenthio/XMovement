/// <summary>
/// Plays surface-aware footstep sounds driven by the citizen model's animation events.
/// Attach to the player GameObject or add to the player prefab.
///
/// Logic mirrors the engine's PlayerController.Footsteps.cs (sbox-public):
///   - 0.2s minimum time between steps
///   - Volume remapped from WishVelocity (slow walking = quieter)
///   - Alternates FootLeft / FootRight by foot ID
///   - Uses the surface under the player (from a ground trace), same as GroundSurface
/// </summary>
public class PlayerFootsteps : Component
{
	[RequireComponent] public Player Player { get; set; }

	/// <summary>Global volume scale for footstep sounds.</summary>
	[Property, Range( 0f, 2f )] public float FootstepVolume { get; set; } = 1f;

	/// <summary>How far down to trace to find the ground surface.</summary>
	[Property] public float TraceDistance { get; set; } = 20f;

	private SkinnedModelRenderer _renderer;
	private TimeSince _timeSinceStep;

	protected override void OnStart()
	{
		_renderer = Player?.WalkController?.BodyModelRenderer;
		if ( !_renderer.IsValid() )
		{
			Log.Warning( "[PlayerFootsteps] No BodyModelRenderer found — footsteps disabled." );
			return;
		}

		_renderer.OnFootstepEvent += OnFootstep;
	}

	protected override void OnDestroy()
	{
		if ( _renderer.IsValid() )
			_renderer.OnFootstepEvent -= OnFootstep;
	}

	void OnFootstep( SceneModel.FootstepEvent e )
	{
		if ( !Player.IsLocalPlayer ) return;

		var controller = Player.WalkController?.Controller;
		if ( controller is null ) return;

		// Engine gate 1: must be on the ground
		if ( !controller.IsOnGround ) return;

		// Engine gate 2: minimum 0.2s between steps (prevents rapid double-firing)
		if ( _timeSinceStep < 0.2f ) return;
		_timeSinceStep = 0;

		// Engine gate 3: volume is remapped from wish velocity — slow walking plays quietly
		float volume = e.Volume * controller.WishVelocity.Length.Remap( 0, 400, 0, 1 );
		if ( volume <= 0.1f ) return;

		// Noclip suppression
		if ( Player.WalkController.IsNoclipping ) return;

		// Find the surface under the player (equivalent to PlayerController.GroundSurface)
		var origin = Player.GameObject.WorldPosition + Vector3.Up * 5f;
		var tr = Scene.Trace
			.Ray( origin, origin + Vector3.Down * TraceDistance )
			.IgnoreGameObjectHierarchy( Player.Body )
			.IgnoreGameObjectHierarchy( Player.GameObject )
			.WithoutTags( "trigger" )
			.Run();

		if ( !tr.Hit || !tr.Surface.IsValid() ) return;

		var surface = tr.Surface;
		// Alternate left/right by foot ID, same as engine
		var sound = e.FootId == 0
			? (surface.SoundCollection.FootLeft ?? surface.GetBaseSurface()?.SoundCollection.FootLeft)
			: (surface.SoundCollection.FootRight ?? surface.GetBaseSurface()?.SoundCollection.FootRight);

		if ( sound is null ) return;

		// Spatialized via the player GO (FollowParent=false matches engine behaviour)
		var handle = GameObject.PlaySound( sound, 0 );
		if ( handle.IsValid() )
		{
			handle.FollowParent = false;
			handle.Volume *= volume * FootstepVolume;
		}

		// Let NPCs hear footsteps
		NpcStimulusSystem.EmitSound( tr.HitPosition, "footstep", volume * FootstepVolume, Player.GameObject );
	}
}
