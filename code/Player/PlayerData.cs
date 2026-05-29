using Sandbox.Diagnostics;
using System;

/// <summary>
/// Holds persistent player information like deaths, kills
/// </summary>
public sealed partial class PlayerData : Component
{
	/// <summary>
	/// Connection Id of the owning player. Derived from Network.Owner.
	/// </summary>
	public Guid PlayerId => Network.Owner?.Id ?? Guid.Empty;
	public long SteamId => (long)(Network.Owner?.SteamId ?? 0);
	public string DisplayName => Network.Owner?.DisplayName ?? "?";

	[Sync( SyncFlags.FromHost )] public int Kills { get; set; }
	[Sync( SyncFlags.FromHost )] public int Deaths { get; set; }

	[Sync( SyncFlags.FromHost )] public bool IsGodMode { get; set; }

	/// <summary>
	/// Which team this player belongs to. -1 = unassigned.
	/// </summary>
	[Sync( SyncFlags.FromHost )] public int TeamIndex { get; set; } = -1;

	public Connection Connection => Network.Owner;

	/// <summary>
	/// Is this player data me?
	/// </summary>
	public bool IsMe => Network.Owner == Connection.Local;

	/// <inheritdoc cref="Connection.Ping"/>
	public float Ping => Connection?.Ping ?? 0;

	/// <summary>
	/// Data for all players
	/// </summary>
	public static IEnumerable<PlayerData> All => Game.ActiveScene.GetAll<PlayerData>();

	/// <summary>
	/// Get player data for a player
	/// </summary>
	public static PlayerData For( Connection connection ) => connection == null ? default : All.FirstOrDefault( x => x.Network.Owner == connection );

	/// <summary>
	/// Get player data for a player's id
	/// </summary>
	public static PlayerData For( Guid playerId )
	{
		return All.FirstOrDefault( x => x.PlayerId == playerId );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	private void RpcAddStat( string identifier, int amount = 1 )
	{
		Sandbox.Services.Stats.Increment( identifier, amount );
	}

	/// <summary>
	/// Called on the host, calls a RPC on the player and adds a stat
	/// </summary>
	public void AddStat( string identifier, int amount = 1 )
	{
		if ( Application.CheatsEnabled ) return;

		Assert.True( Networking.IsHost, "PlayerData.AddStat is host-only!" );

		using ( Rpc.FilterInclude( Network.Owner ) )
		{
			RpcAddStat( identifier, amount );
		}
	}
}
