using Sandbox;
using XMovement;

public sealed class CameraOverrideZoneController : Component, Component.ITriggerListener
{
	[Property] public int Priority { get; set; } = 0;
	[Property] public float Yaw { get; set; } = -1;
	[Property] public float Pitch { get; set; } = 20;
	[Property] public float Distance { get; set; } = 650f;
	[Property] public bool AllowYawOverride { get; set; } = false;
	public int GetZoneId()
	{
		Vector3 pos = GameObject.WorldPosition;
		return (pos.x.FloorToInt() * 5) + (pos.y.FloorToInt() * 31) + (pos.z.FloorToInt() * 17);
	}

	protected override void OnStart()
	{
		base.OnStart();

		ModelRenderer mr = GameObject.Components.Get<ModelRenderer>();

		if (mr != null && mr.IsValid)
		{
			mr.Enabled = false;
		}
	}

	public void OnTriggerEnter( Collider other )
	{
		PlayerMovement mbc = other.GameObject.Components.GetInParentOrSelf<PlayerMovement>();
		if (!other.IsProxy && mbc != null)
		{
			// This is our player!
			if (CameraController.Local != null && CameraController.Local.IsValid && GameObject != null && GameObject.IsValid)
			{
				CameraController.Local.RegisterCameraOverride(GetZoneId(), Priority, Yaw != -1 ? Yaw : GameObject.WorldRotation.Yaw(), Pitch, Distance, AllowYawOverride);
			}
		}
	}

	public void OnTriggerExit (Collider other ) 
	{
		PlayerMovement mbc = other.GameObject.Components.GetInParentOrSelf<PlayerMovement>();
		if ( !other.IsProxy && mbc != null )
		{
			// This is our marble!
			if ( CameraController.Local != null && CameraController.Local.IsValid && GameObject != null && GameObject.IsValid )
			{
				CameraController.Local.UnregisterCameraOverride(GetZoneId());
			}
		}
	}
}
