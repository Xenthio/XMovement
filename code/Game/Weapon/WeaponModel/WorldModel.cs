using System.Threading;

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
		DoTracerEffect( hitPoint, origin );
	}

	/// <summary>
	/// Called during reload to trigger magazine animations (grab, throw, insert).
	/// MagazineAnimator component on this worldmodel drives the sequence.
	/// </summary>
	public void OnReloadStart( CancellationToken ct = default )
	{
		Log.Info( "WorldModel.OnReloadStart called" );
		var magAnimator = GetComponent<MagazineAnimator>();
		if ( magAnimator.IsValid() )
		{
			Log.Info( "MagazineAnimator found, starting animation" );
			magAnimator.PlayReloadAnimation( ct );
		}
		else
		{
			Log.Warning( "MagazineAnimator NOT found on worldmodel" );
		}
	}
}
