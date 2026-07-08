/// <summary>
/// Spawn helper for <see cref="EntityAttribute"/>-tagged components.
/// Equivalent to the old entity system's <c>new HealthKit()</c> or
/// <c>Entity.Create("item_healthkit")</c> — looks up the generated prefab
/// by classname and clones it into the active scene.
///
/// Usage:
/// <code>
/// // By type — compile-time safe, returns the component directly
/// var kit = Entity.Spawn&lt;ItemHealth&gt;( WorldPosition );
///
/// // By classname string — for map I/O, console commands, runtime lookups
/// var go = Entity.Spawn( "item_healthkit", WorldPosition );
/// </code>
///
/// Both overloads are host-only — spawning entities is an authoritative operation.
/// </summary>
public static class Entity
{
	// Cache classname → prefab path lookups so we don't scan TypeLibrary every spawn
	private static readonly Dictionary<string, string> _classnameToPath = new( StringComparer.OrdinalIgnoreCase );
	private static readonly Dictionary<Type, string>   _typeToClassname  = new();

	// -------------------------------------------------------------------------
	// Spawn by type
	// -------------------------------------------------------------------------

	/// <summary>
	/// Spawns the prefab associated with <typeparamref name="T"/> (via its
	/// <see cref="EntityAttribute"/>) and returns the component on the root object.
	/// Returns null if the prefab isn't found or the component is missing.
	/// </summary>
	public static T Spawn<T>( Vector3 position, Rotation? rotation = null, Transform? parent = null )
		where T : Component
	{
		Assert.True( Networking.IsHost, "Entity.Spawn must be called on the host." );

		var classname = GetClassname( typeof(T) );
		if ( classname is null )
		{
			Log.Warning( $"[Entity] {typeof(T).Name} has no [Entity] attribute — can't spawn." );
			return null;
		}

		var go = SpawnByClassname( classname, position, rotation ?? Rotation.Identity );
		return go?.GetComponent<T>();
	}

	// -------------------------------------------------------------------------
	// Spawn by classname
	// -------------------------------------------------------------------------

	/// <summary>
	/// Spawns the prefab registered under <paramref name="classname"/> and returns
	/// its root <see cref="GameObject"/>. Useful for map I/O and console commands.
	/// </summary>
	public static GameObject Spawn( string classname, Vector3 position, Rotation? rotation = null )
	{
		Assert.True( Networking.IsHost, "Entity.Spawn must be called on the host." );
		return SpawnByClassname( classname, position, rotation ?? Rotation.Identity );
	}

	// -------------------------------------------------------------------------
	// Core
	// -------------------------------------------------------------------------

	private static GameObject SpawnByClassname( string classname, Vector3 position, Rotation rotation )
	{
		var path = GetPrefabPath( classname );
		if ( path is null )
		{
			Log.Warning( $"[Entity] No prefab found for classname '{classname}'." );
			return null;
		}

		var prefab = ResourceLibrary.Get<PrefabFile>( path );
		if ( prefab is null )
		{
			Log.Warning( $"[Entity] ResourceLibrary couldn't load '{path}'." );
			return null;
		}

		var prefabScene = SceneUtility.GetPrefabScene( prefab );
		if ( prefabScene is null )
		{
			Log.Warning( $"[Entity] SceneUtility couldn't get prefab scene for '{path}'." );
			return null;
		}

		var go = prefabScene.Clone( new CloneConfig
		{
			Transform    = new Transform( position, rotation ),
			StartEnabled = true,
		} );
		return go;
	}

	// -------------------------------------------------------------------------
	// Lookup helpers
	// -------------------------------------------------------------------------

	private static string GetClassname( Type type )
	{
		if ( _typeToClassname.TryGetValue( type, out var cached ) )
			return cached;

		var attr = TypeLibrary.GetAttribute<EntityAttribute>( type );
		var classname = attr?.Classname;
		_typeToClassname[type] = classname;
		return classname;
	}

	private static string GetPrefabPath( string classname )
	{
		if ( _classnameToPath.TryGetValue( classname, out var cached ) )
			return cached;

		// Try the generated path first — fast, no scan needed
		var generated = $"prefabs/entities/{classname}.prefab";
		if ( ResourceLibrary.Get<PrefabFile>( generated ) is not null )
		{
			_classnameToPath[classname] = generated;
			return generated;
		}

		// Fall back to scanning all prefabs — covers hand-authored prefabs not
		// in prefabs/entities/, and prefabs from other addons
		var match = ResourceLibrary.GetAll<PrefabFile>()
			.FirstOrDefault( p => string.Equals(
				System.IO.Path.GetFileNameWithoutExtension( p.ResourcePath ),
				classname,
				StringComparison.OrdinalIgnoreCase ) );

		var path = match?.ResourcePath;
		_classnameToPath[classname] = path; // cache null too, avoids repeat scans
		return path;
	}
}
