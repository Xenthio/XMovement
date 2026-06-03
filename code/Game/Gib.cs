using Sandbox.Citizen;

/// <summary>
/// Base class for a single gib piece attached to a character.
/// Add multiple of these as children of your player/NPC prefab (disabled by default).
///
/// Supports two modes matching HL1 behaviour:
///   - <b>Bone-linked</b>: the gib snaps to a named bone on detach (good for limbs/head).
///   - <b>Free</b>: spawns at the character's death position with randomised velocity.
///
/// Gib trigger conditions are evaluated by <see cref="CharacterGibManager"/>, which lives on the
/// same GameObject as the health component. You don't call <see cref="Detach"/> directly.
/// </summary>
public sealed class CharacterGib : Component
{
	/// <summary>Damage tags that make this gib fly. Empty = responds to all gib events.</summary>
	[Property] public TagSet GibTags { get; set; }

	/// <summary>Named bone on the character's <see cref="SkinnedModelRenderer"/> to snap to.</summary>
	[Property] public string BoneName { get; set; }

	/// <summary>Optional effect prefab to clone at the detach point (blood puff, etc.).</summary>
	[Property] public GameObject Effect { get; set; }

	/// <summary>How many seconds before this gib destroys itself. 0 = never.</summary>
	[Property] public float Lifetime { get; set; } = 10f;

	/// <summary>
	/// Detach the gib from the character, apply launch force, and start the lifetime timer.
	/// </summary>
	/// <param name="deathPos">World position of the character at death.</param>
	/// <param name="hitPos">Point of the killing hit (used to compute launch direction).</param>
	/// <param name="force">Magnitude of the launch impulse.</param>
	/// <param name="noShrink">If true, skip zeroing the bone scale (avoids a visual pop on some rigs).</param>
	public void Detach( Vector3 deathPos, Vector3 hitPos, float force = 4096f, bool noShrink = false )
	{
		if ( !Game.IsPlaying ) return;

		GameObject.Enabled = true;
		Tags.Add( "effect" );

		// Zero the bone in the source skeleton so it's invisible while the gib flies
		if ( !noShrink && !string.IsNullOrEmpty( BoneName ) )
		{
			var boneGo = GetComponentInParent<SkinnedModelRenderer>()?.GetBoneObject( BoneName );
			if ( boneGo.IsValid() )
			{
				boneGo.Flags = boneGo.Flags.WithFlag( GameObjectFlags.ProceduralBone, true );
				boneGo.WorldScale = 0;
				WorldPosition    = boneGo.WorldPosition + Vector3.Down * 64f;
			}
		}

		// Detach from the character hierarchy
		GameObject.SetParent( null, true );

		// Launch away from the killing hit
		var rb = GetComponent<Rigidbody>( true );
		if ( rb.IsValid() )
		{
			rb.Enabled = true;
			var launchDir = (deathPos - hitPos).IsNearZeroLength ? Vector3.Up : (deathPos - hitPos).Normal;
			rb.ApplyForce( launchDir * force );
		}

		// Spawn visual effect at detach point
		if ( Effect.IsValid() )
			Effect.Clone( WorldPosition );

		if ( Lifetime > 0f )
			Invoke( Lifetime, () => { if ( GameObject.IsValid() ) GameObject.Destroy(); } );
	}
}

/// <summary>
/// Evaluates HL1-style gibbing conditions and fires <see cref="CharacterGib.Detach"/> on all matching
/// child <see cref="CharacterGib"/> components when the character dies.
///
/// <b>HL1 gib rules (recreated):</b>
/// <list type="bullet">
///   <item>Any death where overkill health ≤ <see cref="OverkillThreshold"/> (default –40).</item>
///   <item>Damage tagged <see cref="DamageTags.GibAlways"/>.</item>
///   <item>Damage tagged <see cref="DamageTags.Explosion"/> or <see cref="DamageTags.Crush"/>.</item>
/// </list>
///
/// Add this alongside the health / damageable component. Call <see cref="TryGib"/> from
/// your <c>OnDeath</c> handler.
/// </summary>
public sealed class CharacterGibManager : Component
{
	/// <summary>
	/// Health threshold below zero that triggers gibbing.
	/// HL1 uses –40 for humans, –80 for large NPCs.
	/// </summary>
	[Property] public float OverkillThreshold { get; set; } = -40f;

	/// <summary>Launch force applied to each gib.</summary>
	[Property] public float GibForce { get; set; } = 4096f;

	/// <summary>
	/// Evaluate whether this death qualifies for gibbing, and if so, detach all matching gibs.
	/// </summary>
	/// <param name="finalHealth">Health value at the moment of death (typically ≤ 0).</param>
	/// <param name="damage">The killing blow's damage info.</param>
	/// <param name="deathPos">Character world position at death.</param>
	/// <param name="hitPos">World position of the killing hit.</param>
	/// <returns>True if gibbing was triggered.</returns>
	public bool TryGib( float finalHealth, DamageInfo damage, Vector3 deathPos, Vector3 hitPos )
	{
		if ( !ShouldGib( finalHealth, damage ) )
			return false;

		DoGib( damage, deathPos, hitPos );
		return true;
	}

	/// <summary>HL1 gib condition check.</summary>
	public bool ShouldGib( float finalHealth, DamageInfo damage )
	{
		if ( damage is not null )
		{
			if ( damage.Tags.Has( DamageTags.GibAlways ) )  return true;
			if ( damage.Tags.Has( DamageTags.Explosion ) )  return true;
			if ( damage.Tags.Has( DamageTags.Crush ) )      return true;
		}

		// HL1: overkill beyond threshold
		return finalHealth <= OverkillThreshold;
	}

	/// <summary>Detach all child <see cref="CharacterGib"/> components (or those matching the damage tags).</summary>
	public void DoGib( DamageInfo damage, Vector3 deathPos, Vector3 hitPos )
	{
		foreach ( var gib in Components.GetAll<CharacterGib>( FindMode.EverythingInDescendants ) )
		{
			// If this gib has tag requirements, check them
			if ( gib.GibTags is not null && damage is not null )
			{
				if ( !damage.Tags.HasAny( gib.GibTags ) )
					continue;
			}

			gib.Detach( deathPos, hitPos, GibForce );
		}
	}
}
