using Sandbox;

public sealed class SoundEffectPlayer : Component
{
	public static SoundEffectPlayer Singleton
	{
		get
		{
			if ( !_singleton.IsValid() )
			{
				_singleton = Game.ActiveScene.GetAllComponents<SoundEffectPlayer>().FirstOrDefault();
			}
			return _singleton;
		}
	}
	private static SoundEffectPlayer _singleton = null;

	[Rpc.Broadcast]
	public void PlaySoundAtPosition(string eventPath, Vector3 pos, System.Guid connectionId, bool uiIfLocal = false)
	{
		SoundHandle soundHandle = Sound.Play( eventPath, pos );
		if ( Connection.Local.Id == connectionId && uiIfLocal && soundHandle != null && soundHandle.IsValid )
		{
			soundHandle.SpacialBlend = 0;
			soundHandle.DistanceAttenuation = false;
			soundHandle.Volume = 0.66f;
		}
	}
}
