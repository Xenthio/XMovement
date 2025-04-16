public sealed class Duplicator : Component
{
	[Property] public int MaxDuplicates { get; set; } = 20;
	[Property] public float SecondsPerDuplicate { get; set; } = 1;
	TimeSince TimeSinceLastDuplicated = 999999;

	int dupes = 0;
	protected override void OnStart()
	{
		base.OnStart();
		GameObject.Components.Get<Rigidbody>().Enabled = false;
		GameObject.Components.Get<ModelCollider>().Enabled = false;
	}
	protected override void OnFixedUpdate()
	{
		if ( TimeSinceLastDuplicated >= SecondsPerDuplicate && dupes <= MaxDuplicates )
		{
			dupes++;
			TimeSinceLastDuplicated = 0;
			Duplicate();
		}
	}
	public GameObject Duplicate()
	{

		var copy = Game.ActiveScene.CreateObject();
		foreach ( Component component in this.Components.GetAll() )
		{
			if ( component is Duplicator ) continue;
			var c = copy.Components.Create( TypeLibrary.GetType( component.GetType() ) );
			c.DeserializeImmediately( component.Serialize().AsObject() );
		}
		copy.Transform.Rotation = Transform.Rotation;
		copy.Transform.Position = Transform.Position;
		copy.Transform.Scale = new Vector3( 1.0f, 1, 1 );
		copy.Components.Get<Rigidbody>( true ).Enabled = true;
		copy.Components.Get<ModelCollider>( true ).Enabled = true;
		return copy;
	}
}
