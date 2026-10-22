// Compile-only API probe. Not a runtime mod or an inventory transaction implementation.
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Items;
using Kingmaker.Items;
using Kingmaker.Items.Slots;
using Kingmaker.Mechanics.Entities;
using Kingmaker.PubSubSystem;

namespace ArmouryEisenhorn.TechnicalChecks
{
    public sealed class ProgressionApiProbe : IUnitDeathHandler, IItemsCollectionHandler
    {
        public void HandleUnitDeath(AbstractUnitEntity unit) { }
        public static string EnemyBlueprintId(AbstractUnitEntity unit)
            => unit.Blueprint.AssetGuid.ToString();
        public void HandleItemsAdded(ItemsCollection collection, ItemEntity item, int count) { }
        public void HandleItemsRemoved(ItemsCollection collection, ItemEntity item, int count) { }

        public static bool HasMaterial(ItemsCollection inventory, BlueprintItem material, int count)
            => inventory.Contains(material, count);
        public static void ConsumeMaterial(ItemsCollection inventory, BlueprintItem material, int count)
            => inventory.Remove(material, count);
        public static ItemEntity CreateWeapon(ItemsCollection inventory, BlueprintItem weapon)
            => inventory.Add(weapon);
        public static bool Unequip(ItemSlot slot) => slot.RemoveItem(false, false);
        public static void Equip(ItemSlot slot, ItemEntity replacement) => slot.InsertItem(replacement, false);
        public static void RemoveOldWeapon(ItemsCollection inventory, ItemEntity oldWeapon)
            => inventory.Remove(oldWeapon);
        public static void RecordMilestone(BlueprintUnlockableFlag flag, int value)
        {
            flag.Unlock();
            flag.Value = value;
        }
        public static int ReadMilestone(BlueprintUnlockableFlag flag) => flag.Value;
    }
}
