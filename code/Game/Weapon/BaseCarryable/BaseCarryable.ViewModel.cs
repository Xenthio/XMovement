public partial class BaseCarryable : Component
{
	[Property, Feature( "ViewModel" )] public GameObject ViewModelPrefab { get; set; }

	protected void CreateViewModel()
	{
		if ( ViewModel.IsValid() ) return;

		DestroyViewModel();

		if ( ViewModelPrefab is null ) return;

		var player = Owner;
		if ( player is null || player.WalkController is null || !player.IsLocalPlayer ) return;

		ViewModel = ViewModelPrefab.Clone( new CloneConfig { Parent = GameObject, StartEnabled = false, Transform = global::Transform.Zero } );
		ViewModel.Flags |= GameObjectFlags.NotSaved | GameObjectFlags.NotNetworked | GameObjectFlags.Absolute;

		// Set ShadowRenderType.Off immediately so there's no one-frame shadow flicker on enable
		foreach ( var mr in ViewModel.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndChildren ) )
			mr.RenderType = ModelRenderer.ShadowRenderType.Off;

		ViewModel.Enabled = true;
		ViewModel.Tags.Add( "firstperson", "viewmodel" );

		var vm = ViewModel.GetComponent<ViewModel>();
		if ( vm.IsValid() ) vm.Deploy();
	}

	protected void DestroyViewModel()
	{
		if ( !ViewModel.IsValid() ) return;

		ViewModel?.Destroy();
		ViewModel = default;
	}
}
