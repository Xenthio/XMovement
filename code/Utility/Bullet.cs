using Sandbox.Rendering;

/// <summary>
/// All the information needed to fire a single bullet.
/// Fill this out from anywhere — weapons, NPCs, turrets, explosions — and pass to <see cref="Bullet.Fire"/>.
/// Mirrors Source SDK's FireBulletsInfo_t.
/// </summary>
public struct BulletInfo
{
	/// <summary>World position the bullet originates from.</summary>
	public Vector3 Origin { get; set; }

	/// <summary>Direction the bullet travels (normalised).</summary>
	public Vector3 Direction { get; set; }

	/// <summary>Damage on a clean hit.</summary>
	public float Damage { get; set; }

	/// <summary>Sphere radius used for the trace (larger = more forgiving hits).</summary>
	public float Radius { get; set; }

	/// <summary>Maximum trace distance in world units.</summary>
	public float Range { get; set; }

	/// <summary>Impulse force applied to physics objects on hit.</summary>
	public float Force { get; set; }

	/// <summary>Number of pellets (1 for a regular bullet, higher for shotguns).</summary>
	public int Count { get; set; }

	/// <summary>Spread cone half-angle in degrees. 0 = perfectly accurate.</summary>
	public float Spread { get; set; }

	/// <summary>The GameObject responsible for firing (used for trace ignore + attacker attribution).</summary>
	public GameObject Attacker { get; set; }

	/// <summary>The weapon that fired (used for damage attribution and tracer origin).</summary>
	public GameObject Weapon { get; set; }

	/// <summary>Impact particle prefab override. If null, falls back to per-surface impact prefabs.</summary>
	public GameObject ImpactEffectOverride { get; set; }

	/// <summary>Tags added to the DamageInfo produced by this bullet.</summary>
	public TagSet DamageTags { get; set; }
}

/// <summary>
/// Fires bullets. A static utility usable from weapons, NPCs, turrets, or anything else.
/// All logic — trace, damage, impact effects, physics push — lives here, not in weapon classes.
/// </summary>
public class Bullet
{

	/// <summary>
	/// Sound to play for nearby players when the bullet whizzes past them.
	/// </summary>
	public static string FlybySound { get; } = "bullet_flyby";

	/// <summary>
	/// Fire one or more bullets described by <paramref name="info"/>.
	/// Returns the trace result of the last pellet fired (useful for single-bullet callers).
	/// Must be called on the host for damage; effects are broadcast to all clients.
	/// </summary>
	public static SceneTraceResult Fire( BulletInfo info )
	{
		var count = Math.Max( 1, info.Count );
		var result = default( SceneTraceResult );
		for ( int i = 0; i < count; i++ )
			result = FireOne( info );
		return result;
	}

	static SceneTraceResult FireOne( in BulletInfo info )
	{
		var direction = info.Spread > 0
			? info.Direction.WithAimCone( info.Spread )
			: info.Direction.Normal;

		var scene = Game.ActiveScene;
		var tr = scene.Trace
			.Ray( info.Origin, info.Origin + direction * info.Range )
			.IgnoreGameObjectHierarchy( info.Attacker )
			.IgnoreGameObjectHierarchy( info.Weapon )
			.WithCollisionRules( "bullet" )
			.WithoutTags( "movement" )
			.Radius( info.Radius )
			.UseHitboxes()
			.Run();

		// Extract bone index from hitbox if available
		int boneIndex = -1;
		if ( tr.Hitbox != null && tr.Hitbox.Bone != null )
			boneIndex = tr.Hitbox.Bone.Index;

		// Impact effects + flyby sound — run on client immediately for zero-delay feedback
		BroadcastImpact( tr.EndPosition, tr.Hit, tr.Normal, tr.GameObject, tr.Surface, info.ImpactEffectOverride, boneIndex );
		BroadcastFlyby( info.Origin, tr.EndPosition, info.Attacker );

		// Route host-side work — damage, physics push, stimuli, blood.
		// Runs immediately on host; clients RPC to host via [Rpc.Host].
		ApplyHit(
			tr.Hit,
			tr.HitPosition,
			info.Origin,
			direction * info.Force,
			tr.Body,
			tr.GameObject,
			info.Attacker,
			info.Weapon,
			info.Damage,
			info.DamageTags );

		return tr;
	}

	[Rpc.Host( NetFlags.Unreliable | NetFlags.DiscardOnDelay )]
	static void ApplyHit(
		bool hit,
		Vector3 hitPosition,
		Vector3 origin,
		Vector3 pushForce,
		PhysicsBody body,
		GameObject hitObject,
		GameObject attacker,
		GameObject weapon,
		float damage,
		TagSet damageTags )
	{
		// Physics push
		if ( body.IsValid() && pushForce.LengthSquared > 0f )
			body.ApplyImpulseAt( hitPosition, pushForce * body.Mass );

		if ( !hit || !hitObject.IsValid() ) return;

		// Damage
		var damageable = hitObject.GetComponentInParent<Component.IDamageable>();
		if ( damageable is not null )
		{
			var dmg = new DamageInfo( damage, attacker, weapon )
			{
				Position = hitPosition,
				Origin   = origin,
				Tags     = damageTags ?? new TagSet(),
			};
			damageable.Damage( dmg );
		}

		// Gunshot stimulus so nearby NPCs react
		NpcStimulusSystem.EmitSound( origin, "gunshot", volume: 1f, source: attacker );

		// Blood splat
		if ( hitObject.Tags.HasAny( "npc", "player" ) )
			BloodSystem.Splat( hitPosition, hitPosition - origin, hitObject );
	}

	/// <summary>
	/// Broadcast a bullet whiz/flyby sound to nearby clients.
	/// Skips the shooter's own client. Sound is played at the closest point on the bullet
	/// path to each listener's camera, clamped to the segment.
	/// </summary>
	[Rpc.Broadcast]
	static void BroadcastFlyby( Vector3 origin, Vector3 endPoint, GameObject attacker )
	{
		if ( Application.IsDedicatedServer ) return;

		// Don't play for the shooter
		if ( attacker.IsValid() && attacker.Network.Owner == Connection.Local ) return;

		var cam = Game.ActiveScene.Camera?.WorldPosition ?? Vector3.Zero;
		var dir = (endPoint - origin);
		var len = dir.Length;
		if ( len < 1f ) return;
		var dirN = dir / len;

		// Closest point on the bullet segment to the camera
		var t = MathX.Clamp( Vector3.Dot( cam - origin, dirN ), 70f, len );
		var soundPos = origin + dirN * t;

		Sound.Play( FlybySound, soundPos );
	}

	/// <summary>
	/// Broadcast an impact effect at a hit point. Use this from melee weapons, explosions,
	/// or anything else that needs surface decals/particles without firing a bullet.
	/// </summary>
	[Rpc.Broadcast]
	public static void SpawnImpactEffect(
		Vector3 hitPoint,
		Vector3 normal,
		GameObject hitObject,
		Surface hitSurface,
		GameObject impactOverride = null )
	{
		if ( Application.IsDedicatedServer ) return;
		if ( !hitObject.IsValid() ) return;
		DoImpact( hitPoint, normal, hitObject, hitSurface, impactOverride );
	}

	/// <summary>
	/// Internal: broadcast impact sound + decal. Called by Bullet.Fire (directly) and SpawnImpactEffect (via RPC).
	/// Does NOT include weapon animations, muzzleflashes or shoot sounds — those are the weapon's job.
	/// </summary>
	[Rpc.Broadcast]
	static void BroadcastImpact(
		Vector3 hitPoint,
		bool hit,
		Vector3 normal,
		GameObject hitObject,
		Surface hitSurface,
		GameObject impactOverride,
		int boneIndex = -1 )
	{
		if ( Application.IsDedicatedServer ) return;
		if ( !hit || !hitObject.IsValid() ) return;
		DoImpact( hitPoint, normal, hitObject, hitSurface, impactOverride, boneIndex );
	}

	/// <summary>
	/// Shared impact logic — spawns sound + decal with bone-closest parenting.
	/// Called locally from both BroadcastImpact and SpawnImpactEffect after their RPC guards.
	/// Tries to use hitbox bone mapping first, falls back to closest-bone-by-distance.
	/// </summary>
	static void DoImpact(
		Vector3 hitPoint,
		Vector3 normal,
		GameObject hitObject,
		Surface hitSurface,
		GameObject impactOverride,
		int boneIndex = -1 )
	{
		// Impact sound
		var bulletSound = hitSurface.IsValid()
			? hitSurface.SoundCollection.Bullet ?? hitSurface.GetBaseSurface()?.SoundCollection.Bullet
			: null;
		if ( bulletSound.IsValid() ) Sound.Play( bulletSound, hitPoint );

		var rot = Rotation.LookAt( normal * -1f, Vector3.Random );

		// Particle impact effect (override, then surface lookup)
		var particlePrefab = impactOverride;
		if ( !particlePrefab.IsValid() && hitSurface.IsValid() )
		{
			particlePrefab = hitSurface.PrefabCollection.BulletImpact
				?? hitSurface.GetBaseSurface()?.PrefabCollection.BulletImpact;
		}

		if ( particlePrefab.IsValid() )
		{
			var particleImpact = particlePrefab.Clone( new CloneConfig
			{
				Transform    = new Transform( hitPoint, rot ),
				StartEnabled = true
			} );
		}

		
		// Decal impact (override, then surface lookup)
		var decalPrefab = impactOverride;
		if ( !decalPrefab.IsValid() && hitSurface.IsValid() )
		{
			decalPrefab = hitSurface.PrefabCollection.BulletImpactDecal
				?? hitSurface.GetBaseSurface()?.PrefabCollection.BulletImpactDecal;
		}

		if ( decalPrefab.IsValid() )
		{ 
			var decalImpact = decalPrefab.Clone( new CloneConfig
			{
				Transform    = new Transform( hitPoint, rot ),
				StartEnabled = true
			} );

			// Bone parenting on skinned meshes so decals follow ragdolls
			var skinned = hitObject.GetComponentInChildren<SkinnedModelRenderer>();
			if ( skinned.IsValid() && skinned.CreateBoneObjects )
			{
				// Note this does require bone objects to be enabled on the skinned model renderer
				GameObject closestBone = null;

				// If we have a valid bone index from the hitbox, use that for accurate decal placement
				if ( boneIndex >= 0 )
				{
					closestBone = skinned.GetBoneObject( boneIndex );
				}

				// Else we use the closest bone to the hit point which is less accurate
				if ( !closestBone.IsValid() )
				{
					var bones = skinned.GetBoneTransforms( true );
					var closestDist = float.MaxValue;
					for ( var i = 0; i < bones.Length; i++ )
					{
						var dist = bones[i].Position.Distance( hitPoint );
						if ( dist < closestDist )
						{
							closestDist = dist;
							closestBone = skinned.GetBoneObject( i );
						}
					}
				}

				if ( closestBone.IsValid() )
					decalImpact.SetParent( closestBone, true );
				else
					decalImpact.SetParent( hitObject, true );
			}
			else
			{
				decalImpact.SetParent( hitObject, true );
			}
		}
	}
}
