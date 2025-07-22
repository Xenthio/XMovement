
public partial class BaseCarryable : Component
{
	[Property, Feature( "Inventory" ), Range( 0, 6 )] public int InventorySlot { get; set; } = 0;
	[Property, Feature( "Inventory" )] public int InventoryOrder { get; set; } = 0;
}
