using Sandbox;
namespace XMovement;

public partial class PlayerWalkControllerComplex : Component
{
	public float EyeHeightOffset;
	float LastEyeHeightOffset = 0;

	public void UpdateCrouching()
	{
		DoCrouching();
		Controller.Height = Height + EyeHeightOffset;
		// This moves our feet up when crouching in air
		var delta = LastEyeHeightOffset - EyeHeightOffset;
		if ( !Controller.IsOnGround )
		{
			var delmove = delta;
			delmove *= WorldScale.z;

			var offset = Vector3.Up * delmove;
			if ( !IsNoclipping )
			{
				Controller.MoveTo( Controller.WorldPosition + offset, true );
			}
			else
			{
				Controller.WorldPosition += offset;
			}
		}

		Head.LocalPosition = new Vector3( 0, 0, HeadHeight + EyeHeightOffset );
		LastEyeHeightOffset = EyeHeightOffset;
	}
	public virtual void DoCrouching()
	{
		var eyeHeightOffset = GetEyeHeightOffset();
		EyeHeightOffset = EyeHeightOffset.LerpTo( eyeHeightOffset, Time.Delta * 10f );
	}
	protected float GetEyeHeightOffset()
	{
		if ( IsCrouching ) return -36f;
		return 0f;
	}
}
