using Sandbox.Citizen;
using XMovement;

public partial class BaseCarryable : Component
{
	public interface IEvent : ISceneEvent<IEvent>
	{
		public void OnCreateWorldModel() { }
		public void OnDestroyWorldModel() { }
	}

	[Property, Feature( "WorldModel" )] public GameObject WorldModelPrefab { get; set; }
	[Property, Feature( "WorldModel" )] public GameObject DroppedGameObject { get; set; }
	[Property, Feature( "WorldModel" )] public CitizenAnimationHelper.HoldTypes HoldType { get; set; } = CitizenAnimationHelper.HoldTypes.HoldItem;
	[Property, Feature( "WorldModel" )] public string ParentBone { get; set; } = "hold_r";

	protected void CreateWorldModel()
	{
		var walkController = GetComponentInParent<PlayerWalkControllerComplex>();
		if ( walkController?.BodyModelRenderer is null ) return;

		CreateWorldModel( walkController.BodyModelRenderer );
	}

	public void SetDropped( bool dropped )
	{
		var rb = GetComponent<Rigidbody>( true );
		if ( rb.IsValid() ) rb.Enabled = dropped;

		var col = GetComponent<ModelCollider>( true );
		if ( col.IsValid() ) col.Enabled = dropped;

		var droppedWeapon = GetComponent<DroppedWeapon>( true );
		if ( droppedWeapon.IsValid() ) droppedWeapon.Enabled = dropped;

		if ( DroppedGameObject.IsValid() ) DroppedGameObject.Enabled = dropped;
	}

	public void CreateWorldModel( SkinnedModelRenderer renderer )
	{
		if ( renderer is null ) return;
		if ( WorldModel.IsValid() ) return; // already created

		if ( Networking.IsHost )
			IsItem = false;

		SetDropped( false );

		var worldModel = WorldModelPrefab?.Clone( new CloneConfig
		{
			Parent = renderer.GetBoneObject( ParentBone ) ?? GameObject,
			StartEnabled = true,
			Transform = global::Transform.Zero
		} );

		if ( worldModel.IsValid() )
		{
			worldModel.Flags |= GameObjectFlags.NotSaved | GameObjectFlags.NotNetworked;
			WorldModel = worldModel;
			IEvent.PostToGameObject( WorldModel, x => x.OnCreateWorldModel() );

			// Immediately set correct render type — FixedUpdate/OnUpdate may not run for another frame.
			// Also calls UpdateBodyVisibility in case it can sweep the bone hierarchy.
			var walkController = GetComponentInParent<PlayerWalkControllerComplex>();
			if ( walkController is not null )
			{
				walkController.UpdateBodyVisibility();
			}
			// Belt-and-suspenders: set directly on the worldmodel renderers
			var isFirstPerson = walkController?.CameraMode ==
				XMovement.PlayerWalkControllerComplex.CameraModes.FirstPerson &&
				!walkController.IsProxy;
			var renderType = isFirstPerson
				? ModelRenderer.ShadowRenderType.ShadowsOnly
				: ModelRenderer.ShadowRenderType.On;
			foreach ( var mr in worldModel.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndChildren ) )
				mr.RenderType = renderType;
		}
	}

	protected void DestroyWorldModel()
	{
		if ( WorldModel.IsValid() )
			IEvent.PostToGameObject( WorldModel, x => x.OnDestroyWorldModel() );

		WorldModel?.Destroy();
		WorldModel = default;

		if ( Networking.IsHost )
			IsItem = true;
	}
}
