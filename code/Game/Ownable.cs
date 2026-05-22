/// <summary>
/// Tracks which network connection owns a spawned object.
/// Useful for undo systems, prop protection, and attribution (kill feed, stats).
///
/// Use Ownable.Set(go, connection) after NetworkSpawn to assign ownership.
/// Ported from Sandbox game (MIT License).
/// </summary>
public sealed class Ownable : Component
{
	[Sync( SyncFlags.FromHost )]
	private Guid _ownerId { get; set; }

	[Property, ReadOnly]
	public Connection Owner
	{
		get => Connection.All.FirstOrDefault( c => c.Id == _ownerId );
		set => _ownerId = value?.Id ?? Guid.Empty;
	}

	/// <summary>
	/// Convenience: get-or-add an Ownable on the given GameObject and set its owner.
	/// </summary>
	public static Ownable Set( GameObject go, Connection owner )
	{
		var ownable = go.GetOrAddComponent<Ownable>();
		ownable.Owner = owner;
		return ownable;
	}

	/// <summary>
	/// When true, players can only physgun/toolgun objects they own.
	/// Host is always exempt. Off by default.
	/// </summary>
	[ConVar( "sb.ownership_checks", ConVarFlags.Replicated | ConVarFlags.Server | ConVarFlags.GameSetting )]
	public static bool OwnershipChecks { get; set; } = false;

	public static bool HasAccess( Connection caller, Connection owner )
	{
		if ( !OwnershipChecks ) return true;
		if ( caller is null ) return false;
		if ( caller.HasPermission( "admin" ) ) return true;
		if ( owner is null ) return true;
		return owner == caller;
	}
}

public static class OwnableExtensions
{
	/// <summary>Returns true if the caller has access to interact with this object.</summary>
	public static bool HasAccess( this GameObject go, Connection caller )
	{
		if ( go.Components.TryGet<Ownable>( out var ownable ) )
			return Ownable.HasAccess( caller, ownable.Owner );
		return true;
	}
}
