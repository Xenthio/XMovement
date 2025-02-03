using System;
using System.Runtime.CompilerServices;
using Sandbox;

public sealed class SetMeshSkin : Component, Component.INetworkSpawn
{
	[Property] public SkinnedModelRenderer skinnedModelRenderer;

	public void OnNetworkSpawn( Connection owner )
	{
		var clothing = new ClothingContainer();
		clothing.Deserialize( owner.GetUserData( "avatar" ) );
		clothing.Apply( skinnedModelRenderer );
	}
}
