using Sandbox;
using XMovement;

namespace XMovement;
public sealed class LookDirectionController : Component
{
	public static LookDirectionController Local
	{
		get
		{
			if ( !_local.IsValid() && PlayerWalkControllerSimple.Local != null )
			{
				_local = PlayerWalkControllerSimple.Local.Components.GetInChildrenOrSelf<LookDirectionController>();
			}
			return _local;
		}
	}
	private static LookDirectionController _local = null;


	protected override void OnUpdate()
	{
		if ( CameraController.Local != null && CameraController.Local.IsValid && CameraController.Local.Head != null && CameraController.Local.Head.IsValid)
		{
			if ( GameObject != null && GameObject.IsValid )
			{
				GameObject.WorldPosition = CameraController.Local.Head.WorldPosition;
			}
		}
	}
}
