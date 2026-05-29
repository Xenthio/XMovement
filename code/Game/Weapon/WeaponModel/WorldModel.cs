public sealed class WorldModel : WeaponModel
{
	public override void OnAttack()
	{
		Renderer?.Set( "b_attack", true );

		DoMuzzleEffect();
		DoEjectBrass();
	}

	public override void CreateRangedEffects( BaseWeapon weapon, Vector3 hitPoint, Vector3? origin )
	{
		// Only fire tracer from worldmodel if there's no viewmodel visible.
		// Viewmodel handles its own tracer from its muzzle; worldmodel covers third-person and spectators.
		if ( weapon.ViewModel.IsValid() ) return;

		DoTracerEffect( hitPoint, origin );
	}
}
