using ItemChanger;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnRando.Rando;

internal class MultiUIDef : UIDef
{
    public readonly List<UIDef>? uiDefs;

    [JsonConstructor]
    private MultiUIDef() { }

    internal MultiUIDef(List<UIDef> uiDefs) { this.uiDefs = [.. uiDefs]; }

    public override string GetPostviewName() => string.Join(", ", uiDefs.Select(ui => ui.GetPostviewName()).ToArray());

    public override string GetPreviewName() => string.Join(", ", uiDefs.Select(ui => ui.GetPreviewName()).ToArray());

    public override string? GetShopDesc() => uiDefs!.Count == 1 ? uiDefs[0].GetShopDesc() : $"It's a bundle deal for {uiDefs.Count} items! Not sold separately";

    public override Sprite GetSprite() => uiDefs![0].GetSprite();

    public override void SendMessage(MessageType type, Action? callback = null) => throw new InvalidOperationException("Not supported on MultiUIDef");
}
