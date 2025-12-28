using ItemChanger;
using ItemChanger.Placements;
using ItemChanger.Tags;
using ItemChanger.Util;
using Modding;
using System.Collections.Generic;
using System.Linq;

namespace UnRando.Rando;

internal class UnRandoPlacementTag : Tag
{
    public string? PlacementName;
}

internal class UnRandoCheck : AbstractItem
{
    public UnRandoCheck()
    {
        name = nameof(UnRandoCheck);

        // Don't display un-rando checks.
        var interop = AddTag<InteropTag>();
        interop.Message = "RecentItems";
        interop.Properties.Add("IgnoreItem", true);
    }

    public override AbstractItem Clone() => new UnRandoCheck();

    private static AbstractPlacement? GetRealPlacement(bool forReal)
    {
        var mod = UnRandoModule.Get()!;

        int checksRequired = forReal ? ++mod.ChecksObtained : (mod.ChecksObtained + 1);
        UnRandoLocation loc = new(checksRequired);
        return ItemChanger.Internal.Ref.Settings.Placements[loc.name];
    }

    public override bool GiveEarly(string containerType) => GetRealPlacement(false)?.Items.Any(i => i.GiveEarly(containerType)) ?? false;

    private static AbstractItem Nothing() => Finder.GetItem("Lumafly_Escape")!;

    private static bool? _recentItemsInstalled;
    private static bool RecentItemsInstalled()
    {
        _recentItemsInstalled ??= ModHooks.GetMod("RecentItems") is Mod;
        return _recentItemsInstalled.Value;
    }

    private static readonly List<string> RANDOM_PLACES = [
        "somewhere",
        "over there",
        "thataway",
        "thisaway",
        "the store",
        "somewhere else",
        "anywhere",
        "nowhere"
    ];

    private static string RandomPlace() => RANDOM_PLACES[UnityEngine.Random.Range(0, RANDOM_PLACES.Count)];

    private static void AddRecentItemsTag(AbstractItem item, string? scene)
    {
        var recentItems = item.AddTag<InteropTag>();
        recentItems.Message = "RecentItems";
        recentItems.Properties.Add(
            "DisplaySource",
            scene != null ? RecentItemsDisplay.AreaName.LocalizedCleanAreaName(scene) : RandomPlace());
    }

    private static void RemoveItemSyncTags(TaggableObject obj)
    {
        // TaggableObject should have a 'RemoveTag(tag)' function.
        List<Tag> keep = [];
        foreach (var tag in obj.GetTags<IInteropTag>())
        {
            if (tag.Message != "SyncedItemTag") keep.Add((tag as Tag)!);
        }

        obj.RemoveTags<IInteropTag>();
        obj.AddTags(keep);
    }

    public override void GiveImmediate(GiveInfo info)
    {
        var p = GetRealPlacement(true);
        var callback = info.Callback;

        var checkPlacement = ItemChanger.Internal.Ref.Settings.Placements[GetTag<UnRandoPlacementTag>()!.PlacementName!];
        var scene = (checkPlacement as IPrimaryLocationPlacement)?.Location.sceneName;

        // Remove item sync tags for the actual items.
        List<AbstractItem> items = [.. p?.Items ?? [Nothing()]];
        if (ModHooks.GetMod("ItemSyncMod") is Mod)
        {
            items.ForEach(RemoveItemSyncTags);
            if (p != null) RemoveItemSyncTags(p);
        }

        List<AbstractItem> toInsert = [];
        foreach (var item in items)
        {
            if (RecentItemsInstalled()) AddRecentItemsTag(item, scene);

            // Place refillables on location.
            var tag = item.GetTag<PersistentItemTag>();
            if (tag != null && tag.Persistence == Persistence.SemiPersistent)
            {
                // Place a pre-obtained copy of this item at this location.
                var clone = item.Clone();
                clone.SetObtained();
                toInsert.Add(clone);
            }
        }

        ItemUtility.GiveSequentially(items, p, info, () =>
        {
            callback?.Invoke(this);

            if (checkPlacement is ShopPlacement shop)
            {
                toInsert.ForEach(i => shop.AddItemWithCost(i, 1));
                shop.RemoveTags<MultiPreviewRecordTag>();
            }
            else checkPlacement.Items.InsertRange(0, toInsert);
        });

        // Null out the callback to prevent early control.
        UIDef = null;
        info.Callback = null;
    }

    public override void ResolveItem(GiveEventArgs args)
    {
        args.Item = this;

        var p = GetRealPlacement(false);
        List<UIDef> uiDefs = [];
        if (p != null)
        {
            foreach (var item in p.Items)
            {
                GiveEventArgs delegateArgs = new(item, item, args.Placement, args.Info, args.OriginalState);
                item.ResolveItem(delegateArgs);
                uiDefs.Add(delegateArgs.Item!.UIDef!);
            }
        }
        else uiDefs.Add(Nothing().GetResolvedUIDef()!);

        UIDef = new MultiUIDef(uiDefs);
    }
}
