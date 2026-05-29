/// <summary>
/// String constants for damage tags. Use these when building DamageInfo.Tags
/// to categorise damage types for kill feed icons, gibbing, effects, and gameplay logic.
/// </summary>
public static class DamageTags
{
	/// <summary>Damage hit the head. Used for headshot bonuses and kill feed icons.</summary>
	public const string Headshot = "head";

	/// <summary>Crushing / physics damage (prop squish, etc.).</summary>
	public const string Crush = "crush";

	/// <summary>Explosion damage (grenades, RPG). Applies knockback.</summary>
	public const string Explosion = "explosion";

	/// <summary>Electrical / shock damage.</summary>
	public const string Shock = "shock";

	/// <summary>Fall damage.</summary>
	public const string Fall = "fall";

	/// <summary>Always gib the target regardless of health.</summary>
	public const string GibAlways = "gib_always";

	/// <summary>Self-damage is not reduced (e.g. full rocket splash on self).</summary>
	public const string FullSelfDamage = "full_self_damage";

	/// <summary>Bullet / hitscan damage.</summary>
	public const string Bullet = "bullet";

	/// <summary>Melee damage.</summary>
	public const string Melee = "melee";

	/// <summary>Sharp / slash damage.</summary>
	public const string Slash = "slash";

	/// <summary>Fire / burn damage.</summary>
	public const string Burn = "burn";

	/// <summary>Drown damage (underwater).</summary>
	public const string Drown = "drown";

	/// <summary>Radiation / toxic damage.</summary>
	public const string Radiation = "radiation";

	/// <summary>Acid damage.</summary>
	public const string Acid = "acid";

	/// <summary>Sonic / concussion damage.</summary>
	public const string Sonic = "sonic";

	/// <summary>Energy beam damage.</summary>
	public const string EnergyBeam = "energy_beam";

	/// <summary>Poison / nervegas damage (slow DoT).</summary>
	public const string Poison = "poison";

	/// <summary>Paralyse.</summary>
	public const string Paralyse = "paralyse";

	/// <summary>Prevent physics force being applied on this hit.</summary>
	public const string NoKnockback = "no_knockback";

	/// <summary>Target is dissolved / disintegrated on death (no ragdoll, no gib).</summary>
	public const string Dissolve = "dissolve";

	/// <summary>Damage inflicted by a vehicle (run-over, collision).</summary>
	public const string Vehicle = "vehicle";

	/// <summary>Plasma / energy ball impact.</summary>
	public const string Plasma = "plasma";

	/// <summary>Physics impact damage player or prop colliding at speed.</summary>
	public const string Impact = "impact";

	/// <summary>Bullet penetrated a surface before hitting (reduced damage, different effects).</summary>
	public const string BulletPenetrated = "bullet_penetrated";


	/// <summary>Returns true if any tag in the set should trigger gibbing logic.</summary>
	public static bool IsGibType( TagSet tags )
		=> tags.HasAny( Crush, Explosion, GibAlways );
}
