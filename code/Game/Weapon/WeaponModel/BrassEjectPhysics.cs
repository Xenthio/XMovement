/// <summary>
/// Cheap brass ejection physics. Near-identical to TTT's ParticleSimplePhysicsComponent.
/// base.OnParticleStep handles position, gravity (via ForceDirection), and built-in collision.
/// We just halve speed on hit and manage angles/tumble.
/// StartVelocity/StartAngles/StartAngularVelocity set by DoEjectBrass before enabling.
/// </summary>
[Title( "Brass Eject Physics" )]
[Category( "Particles" )]
[Icon( "cases" )]
public sealed class BrassEjectPhysics : ParticleController
{
	[Property] public Vector3 StartVelocity { get; set; }
	[Property] public Vector3 StartAngularVelocity { get; set; }
	[Property] public Angles  StartAngles { get; set; }
	[Property, ResourceType( "sound" )] public string ImpactSound { get; set; } = "prefabs/effects/shell_case_bounce.wav.sound";

	protected override void OnParticleCreated( Particle p )
	{
		base.OnParticleCreated( p );
		p.Angles = StartAngles;
		p.Set<Vector3>( "angvel", StartAngularVelocity );
		p.Set<int>( "init", 0 ); // StartVelocity applied on first step
	}

	protected override void OnParticleStep( Particle particle, float delta )
	{
		// Apply StartVelocity on first step — by then caller has definitely set it
		if ( particle.Get<int>( "init" ) == 0 )
		{
			particle.Velocity += StartVelocity;
			particle.Set<int>( "init", 1 );
		}
		base.OnParticleStep( particle, delta );

		var tr = Scene.Trace
			.Ray( particle.Position, particle.Position + particle.Velocity * Time.Delta )
			.Radius( particle.Radius )
			.WithoutTags( ParticleEffect.CollisionIgnore )
			.Run();

		if ( tr.Hit )
		{
			particle.Velocity *= 0.5f;

			if ( !particle.Velocity.z.AlmostEqual( 0, 8f ) )
			{
				particle.Angles = new Angles( 0, Game.Random.Float( 0, 360f ), 0 );
				particle.Set<Vector3>( "angvel", Vector3.Random * 300 );
				if ( !string.IsNullOrWhiteSpace( ImpactSound ) )
					Sound.Play( ImpactSound, particle.Position );
			}
			else
			{
				particle.Angles   = new Angles( 0, particle.Angles.yaw, 0 );
				particle.Velocity = Vector3.Zero;
				particle.Set<Vector3>( "angvel", Vector3.Zero );
			}
		}

		if ( particle.LastHitTime != Time.Now && !particle.Velocity.z.AlmostEqual( 0, 8f ) )
		{
			var angvel = particle.Get<Vector3>( "angvel" );
			particle.Angles += new Angles( angvel.x, angvel.y, angvel.z ) * Time.Delta;
		}
	}
}
