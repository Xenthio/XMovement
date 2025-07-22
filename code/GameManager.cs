public sealed partial class GameManager : GameObjectSystem<GameManager>, Component.INetworkListener, ISceneStartup
{
	public GameManager( Scene scene ) : base( scene )
	{
	}

	void ISceneStartup.OnHostInitialize()
	{
		if ( !Scene.WantsSystemScene ) return;

		Scene.NavMesh.AgentRadius = 20;
		Scene.NavMesh.AgentHeight = 72;
		Scene.NavMesh.IsEnabled = true;

	}

	void Component.INetworkListener.OnActive( Connection channel )
	{
		channel.CanSpawnObjects = false;

		var playerData = CreatePlayerInfo( channel );
		SpawnPlayer( playerData );
	}

	/// <summary>
	/// Called when someone leaves the server. This will only be called for the host.
	/// </summary>
	void Component.INetworkListener.OnDisconnected( Connection channel )
	{
		var pd = PlayerData.For( channel );
		if ( pd is not null )
		{
			pd.GameObject.Destroy();
		}
	}

	private PlayerData CreatePlayerInfo( Connection channel )
	{
		var go = new GameObject( true, $"PlayerInfo - {channel.DisplayName}" );
		var data = go.AddComponent<PlayerData>();
		data.SteamId = (long)channel.SteamId;
		data.PlayerId = channel.Id;
		data.DisplayName = channel.DisplayName;

		go.NetworkSpawn( null );
		go.Network.SetOwnerTransfer( OwnerTransfer.Fixed );

		return data;
	}

	public void SpawnPlayer( Connection connection ) => SpawnPlayer( PlayerData.For( connection ) );

	public void SpawnPlayer( PlayerData playerData )
	{
		Assert.NotNull( playerData, "PlayerData is null" );
		Assert.True( Networking.IsHost, $"Client tried to SpawnPlayer: {playerData.DisplayName}" );

		// does this connection already have a player?
		if ( Scene.GetAll<Player>().Where( x => x.Network.Owner?.Id == playerData.PlayerId ).Any() )
			return;

		// Find a spawn location for this player
		var startLocation = FindSpawnLocation().WithScale( 1 );

		// Spawn this object and make the client the owner
		var playerGo = GameObject.Clone( "/prefabs/player.prefab", new CloneConfig { Name = playerData.DisplayName, StartEnabled = false, Transform = startLocation } );

		Log.Info( playerGo );

		var player = playerGo.Components.Get<Player>( true );
		player.PlayerData = playerData;

		var owner = Connection.Find( playerData.PlayerId );
		playerGo.NetworkSpawn( owner );

		IPlayerEvent.PostToGameObject( player.GameObject, x => x.OnSpawned() );
	}

	public void SpawnPlayerDelayed( PlayerData playerData )
	{
		GameTask.RunInThreadAsync( async () =>
		{
			await Task.Delay( 4000 );
			await GameTask.MainThread();
			if ( Current is not null )
				Current.SpawnPlayer( playerData );
		} );
	}

	/// <summary>
	/// In the editor, spawn the player where they're looking
	/// </summary>
	public static Transform EditorSpawnLocation { get; set; }

	/// <summary>
	/// Find the most appropriate place to respawn
	/// </summary>
	Transform FindSpawnLocation()
	{

		//
		// If we have any SpawnPoint components in the scene, then use those
		//
		var spawnPoints = Scene.GetAllComponents<SpawnPoint>().ToArray();

		if ( spawnPoints.Length == 0 )
		{
			if ( Application.IsEditor && !EditorSpawnLocation.Position.IsNearlyZero() )
			{
				return EditorSpawnLocation;
			}

			return Transform.Zero;
		}

		var players = Scene.GetAll<Player>();

		if ( !players.Any() )
		{
			return Random.Shared.FromArray( spawnPoints ).Transform.World;
		}

		//
		// Find spawnpoint furthest away from any players
		// TODO: in the future we may want a different logic, as spawning far away is not necessarily good.
		// But good enough for now and also reduces chances of players from spawning on top of  or inside each other.
		//
		SpawnPoint spawnPointFurthestAway = null;
		float spawnPointFurthestAwayDistanceSqr = float.MinValue;

		foreach ( var spawnPoint in spawnPoints )
		{
			float closestPlayerDistanceToSpawnpointSqr = float.MaxValue;

			foreach ( var player in players )
			{
				float playerDistanceToSpawnPointSqr = (spawnPoint.Transform.World.Position - player.Transform.World.Position).LengthSquared;

				if ( playerDistanceToSpawnPointSqr < closestPlayerDistanceToSpawnpointSqr )
				{
					closestPlayerDistanceToSpawnpointSqr = playerDistanceToSpawnPointSqr;
				}
			}

			if ( closestPlayerDistanceToSpawnpointSqr > spawnPointFurthestAwayDistanceSqr )
			{
				spawnPointFurthestAwayDistanceSqr = closestPlayerDistanceToSpawnpointSqr;
				spawnPointFurthestAway = spawnPoint;
			}
		}

		return spawnPointFurthestAway.Transform.World;
	}

	[Rpc.Broadcast]
	private static void SendMessage( string msg )
	{
		Log.Info( msg );
	}

	/// <summary>
	/// Called on the host when a played is killed
	/// </summary>
	public void OnDeath( Player player, DamageInfo dmg )
	{
		Assert.True( Networking.IsHost );

		Assert.True( player.IsValid(), "Player invalid" );
		Assert.True( player.PlayerData.IsValid(), $"{player.GameObject.Name}'s PlayerData invalid" );

		var weapon = dmg.Weapon;
		var attackerData = PlayerData.For( dmg.InstigatorId );
		bool isSuicide = attackerData == player.PlayerData;

		if ( attackerData.IsValid() && !isSuicide )
		{
			Assert.True( weapon.IsValid(), $"Weapon invalid. (Attacker: {attackerData.DisplayName}, Victim: {player.DisplayName})" );

			attackerData.Kills++;
			attackerData.AddStat( $"kills" );

			if ( weapon.IsValid() )
			{
				attackerData.AddStat( $"kills.{weapon.Name}" );
			}
		}

		player.PlayerData.Deaths++;

		string attackerName = attackerData.IsValid() ? attackerData.DisplayName : dmg.Attacker?.Name;
		if ( string.IsNullOrEmpty( attackerName ) )
			SendMessage( $"{player.DisplayName} died (tags: {dmg.Tags})" );
		else if ( weapon.IsValid() )
			SendMessage( $"{attackerName} killed {(isSuicide ? "self" : player.DisplayName)} with {weapon.Name} (tags: {dmg.Tags})" );
		else
			SendMessage( $"{attackerName} killed {(isSuicide ? "self" : player.DisplayName)} (tags: {dmg.Tags})" );
	}
}
