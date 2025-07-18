using XMovement;

namespace XPSBase;

public class PlayerComponent : Component
{
	[Property, Group( "Player" )] public PlayerWalkControllerComplex WalkController;
	[Property, Group( "Player" )] public PlayerMovement Movement;
	protected override void OnStart()
	{
		base.OnStart();
	}
}
