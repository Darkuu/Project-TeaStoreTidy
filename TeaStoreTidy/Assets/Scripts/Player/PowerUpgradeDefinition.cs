using UnityEngine;

public enum PowerUpgradeEffect
{
    IncreaseInventoryCapacity,
    ScentTrail
}

[CreateAssetMenu(menuName = "Tea Store Tidy/Power Upgrade", fileName = "NewPowerUpgrade")]
public sealed class PowerUpgradeDefinition : ScriptableObject
{
    [Tooltip("Unique ID used to keep this upgrade's progress in save files.")]
    public string id = "inventory_capacity";
    public string displayName = "Increase Inventory Size";
    public PowerUpgradeEffect effect = PowerUpgradeEffect.IncreaseInventoryCapacity;
    [Min(1)] public int amountPerPurchase = 1;
    [Min(1)] public int startingCost = 1;
    [Min(1f)] public float costMultiplier = 1.5f;
    [Tooltip("One-time unlocks cannot be purchased again after the first purchase.")]
    public bool oneTimeUnlock;
}
