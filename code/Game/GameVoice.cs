/// <summary>
/// Voice chat with per-player mute support.
/// Drop this on your GameManager GameObject.
///
/// Players can mute others via <see cref="Mute(SteamId)"/>. Mutes are local-only (client-side).
/// </summary>
public partial class GameVoice : Voice
{
	/// <summary>Set of locally-muted Steam IDs.</summary>
	public static HashSet<SteamId> MutedList { get; } = new();

	/// <summary>Toggle mute for a player.</summary>
	public static void Mute( SteamId id )
	{
		if ( !MutedList.Add( id ) )
			MutedList.Remove( id );
	}

	/// <summary>Returns true if the given Steam ID is locally muted.</summary>
	public static bool IsMuted( SteamId id ) => MutedList.Contains( id );

	protected override bool ShouldHearVoice( Connection connection )
		=> !MutedList.Contains( connection.SteamId );
}
