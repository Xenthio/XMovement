public class PlayerInventory : Component
{
	[RequireComponent] public Player Player { get; set; }
	public List<BaseCarryable> Weapons => GetComponentsInChildren<BaseCarryable>( true ).OrderBy( x => x.InventorySlot ).ThenBy( x => x.InventoryOrder ).ToList();
	[Sync] public BaseCarryable ActiveWeapon { get; private set; }
}
