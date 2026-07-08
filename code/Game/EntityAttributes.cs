using System;

/// <summary>
/// Marks a <see cref="Component"/> subclass as an auto-generated prefab entity.
///
/// When this attribute is present, the XenGameKit editor system will automatically
/// create and keep up to date a .prefab file for this component at hotload time.
/// The prefab is placed in <c>Assets/prefabs/entities/</c> and named after
/// <paramref name="classname"/>.
///
/// Think of this as the modern equivalent of the old entity-system's
/// <c>[Library("classname"), HammerEntity]</c> — except instead of an FGD
/// entry, you get a real s&amp;box prefab you can drag into a scene or spawn
/// through the scene hierarchy right-click menu.
///
/// The generator writes the component + a <c>Sandbox.Prop</c> (when
/// <see cref="PropModelAttribute"/> is present). Any colliders or other
/// components should be added via <c>[RequireComponent]</c> on the class itself
/// — the generator only writes what it's explicitly told about.
///
/// <example>
/// <code>
/// [Entity( "item_healthvial" ), PropModel( "models/props/health_pickup/health_pickup.vmdl" )]
/// [Title( "Health Vial" ), Category( "Items" )]
/// public class ItemHealthVial : BaseItem { ... }
/// </code>
/// </example>
/// </summary>
[AttributeUsage( AttributeTargets.Class, AllowMultiple = false, Inherited = false )]
public sealed class EntityAttribute : Attribute
{
	/// <summary>
	/// The entity classname (e.g. "item_healthkit", "weapon_pistol").
	/// This becomes the prefab file name and the key used to detect orphaned prefabs.
	/// </summary>
	public string Classname { get; }

	public EntityAttribute( string classname )
	{
		Classname = classname;
	}
}

/// <summary>
/// Sets the model path for the <see cref="Sandbox.Prop"/> component that the
/// entity prefab generator places on the generated prefab.
///
/// If omitted, no Prop or model renderer is added — the entity will be
/// trigger/logic only (invisible in the scene until you add visuals manually).
///
/// The path should be a relative asset path the same way you'd set it in the
/// scene inspector, e.g. <c>"models/weapons/w_ak.vmdl"</c>.
/// </summary>
[AttributeUsage( AttributeTargets.Class, AllowMultiple = false, Inherited = false )]
public sealed class PropModelAttribute : Attribute
{
	public string ModelPath { get; }

	public PropModelAttribute( string modelPath )
	{
		ModelPath = modelPath;
	}
}

/// <summary>
/// Instructs the entity prefab generator to include a trigger <see cref="Sandbox.BoxCollider"/>
/// on the generated prefab. Required for any entity that inherits from <see cref="BaseItem"/>
/// since <see cref="BaseItem"/> uses <c>[RequireComponent] Collider</c> for pickup detection.
///
/// <paramref name="cx"/>, <paramref name="cy"/>, <paramref name="cz"/> set the collider center.
/// <paramref name="sx"/>, <paramref name="sy"/>, <paramref name="sz"/> set the collider scale (size).
/// </summary>
[AttributeUsage( AttributeTargets.Class, AllowMultiple = false, Inherited = false )]
public sealed class TriggerBoxAttribute : Attribute
{
	public float CenterX { get; }
	public float CenterY { get; }
	public float CenterZ { get; }
	public float SizeX   { get; }
	public float SizeY   { get; }
	public float SizeZ   { get; }

	/// <param name="cx">Center X</param>
	/// <param name="cy">Center Y</param>
	/// <param name="cz">Center Z</param>
	/// <param name="sx">Size X</param>
	/// <param name="sy">Size Y</param>
	/// <param name="sz">Size Z</param>
	public TriggerBoxAttribute( float cx = 0, float cy = 0, float cz = 10,
	                            float sx = 24, float sy = 24, float sz = 20 )
	{
		CenterX = cx; CenterY = cy; CenterZ = cz;
		SizeX   = sx; SizeY   = sy; SizeZ   = sz;
	}
}

