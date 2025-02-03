using Sandbox;
using System;

public sealed class PlayerLocalDataManager : Component
{
	public static string FILENAME = "waveparkour.sav";
	public static int SAVE_VERSION = 1;

	public static PlayerLocalDataManager Singleton
	{
		get
		{
			if ( !_singleton.IsValid() )
			{
				_singleton = Game.ActiveScene.GetAllComponents<PlayerLocalDataManager>().FirstOrDefault();
			}
			return _singleton;
		}
	}
	private static PlayerLocalDataManager _singleton = null;

	[Serializable]
	public class MarbleSave
	{
		public int SaveVersion { get; set; }
		public int Coins { get; set; }
		public int EquippedSkin {  get; set; }
		public int EquippedHat { get; set; }
		public int EquippedTrail { get; set; }

		public List<int> OwnedSkins { get; set; }
		public List<int> OwnedHats { get; set; }
		public List<int> OwnedTrails { get; set; }

		public long MutedUntilEpoch { get; set; }
		public bool RandomizeSkinEachRound {  get; set; }

		public int OwnedCosmeticsQuantity()
		{
			return OwnedSkins.Count + OwnedHats.Count + OwnedTrails.Count;
		}

		public MarbleSave()
		{
			this.SaveVersion = SAVE_VERSION;
			this.Coins = 0;
			this.EquippedSkin = 0;
			this.EquippedHat = 0;
			this.EquippedTrail = 0;

			this.MutedUntilEpoch = -1;
			this.RandomizeSkinEachRound = false;

			this.OwnedSkins = new();
			this.OwnedHats = new();
			this.OwnedTrails = new();
		}
	}

	public MarbleSave LoadedSave {  get; set; }

	void InitSave()
	{
		LoadedSave = new MarbleSave();
		ProcessSaveOnLoad();
	}

	void ProcessSaveOnLoad()
	{
		if (!LoadedSave.OwnedSkins.Contains(0))
		{
			LoadedSave.OwnedSkins.Add(0);
		}

		if (!LoadedSave.OwnedHats.Contains(0))
		{
			LoadedSave.OwnedHats.Add(0);
		}

		if (!LoadedSave.OwnedTrails.Contains(0))
		{
			LoadedSave.OwnedTrails.Add(0);
		}

		if (!LoadedSave.OwnedSkins.Contains(LoadedSave.EquippedSkin))
		{
			// someone's getting cheeky
			LoadedSave.EquippedSkin = 0;
		}

		if ( !LoadedSave.OwnedHats.Contains( LoadedSave.EquippedHat ) )
		{
			// someone's getting cheeky
			LoadedSave.EquippedHat = 0;
		}

		if ( !LoadedSave.OwnedTrails.Contains( LoadedSave.EquippedTrail ) )
		{
			// someone's getting cheeky
			LoadedSave.EquippedTrail = 0;
		}

		WriteSave();
	}

	void LoadSave()
	{
		Log.Info( "Attempting to load save." );
		MarbleSave s = DataHelper.ReadJson<MarbleSave>( FILENAME );
		if (s != null)
		{
			LoadedSave = s;
			ProcessSaveOnLoad();
			Log.Info( "Loaded save. Coins: " + LoadedSave.Coins.ToString() );
		} else
		{
			Log.Info( "Creating new save data." );
			InitSave();
		}
	}

	public void WriteSave()
	{
		DataHelper.WriteJson<MarbleSave>(FILENAME, LoadedSave);
	}

	protected override void OnStart()
	{
		base.OnStart();

		// use DataHelper to load PlayerSave from local save data.
		LoadSave();
	}

	public void AddCoins(int delta, bool doWriteSave = true)
	{
		if ( LoadedSave == null || delta <= 0 ) return;

		LoadedSave.Coins += delta;

		if ( doWriteSave )
		{
			WriteSave();
		}
	}

	public bool CanSpendCoins(int delta)
	{
		if ( LoadedSave == null ) return false;

		return LoadedSave.Coins >= delta;
	}

	public void RemoveCoins(int delta)
	{
		if (LoadedSave == null || delta <= 0) return;

		LoadedSave.Coins -= delta;
		WriteSave();
	}

	public void MuteForDay()
	{
		if (LoadedSave != null)
		{
			LoadedSave.MutedUntilEpoch = System.DateTime.UtcNow.AddDays( 3 ).GetEpoch();
		}
	}

	public void MutePermanently()
	{
		if (LoadedSave != null)
		{
			LoadedSave.MutedUntilEpoch = 1;
		}
	}

	public void Unmute()
	{
		if (LoadedSave != null)
		{
			LoadedSave.MutedUntilEpoch = -1;
		}
	}

	public bool GetCurrentlyMuted()
	{
		if ( LoadedSave != null )
		{
			if (LoadedSave.MutedUntilEpoch < 0)
			{
				return false;
			}

			if (LoadedSave.MutedUntilEpoch == 1)
			{
				return true;
			}
			 
			return LoadedSave.MutedUntilEpoch > System.DateTime.UtcNow.GetEpoch();
		}
		return false;
	}

	public bool GetRandomizeSkins()
	{
		if (LoadedSave != null)
		{
			return LoadedSave.RandomizeSkinEachRound;
		}
		return false;
	}

	public void ToggleRandomizeSkins()
	{
		if (LoadedSave != null)
		{
			LoadedSave.RandomizeSkinEachRound = !LoadedSave.RandomizeSkinEachRound;
			WriteSave();
		}
	}
}
