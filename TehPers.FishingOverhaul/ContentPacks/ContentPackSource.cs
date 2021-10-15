﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using StardewModdingAPI;
using TehPers.Core.Api.Content;
using TehPers.Core.Api.Items;
using TehPers.Core.Api.Json;
using TehPers.FishingOverhaul.Api.Content;
using TehPers.FishingOverhaul.Config.ContentPacks;

namespace TehPers.FishingOverhaul.ContentPacks
{
    internal sealed class ContentPackSource : IFishingContentSource
    {
        private readonly IModHelper helper;
        private readonly IMonitor monitor;
        private readonly IJsonProvider jsonProvider;

        public ContentPackSource(IModHelper helper, IMonitor monitor, IJsonProvider jsonProvider)
        {
            this.helper = helper ?? throw new ArgumentNullException(nameof(helper));
            this.monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            this.jsonProvider =
                jsonProvider ?? throw new ArgumentNullException(nameof(jsonProvider));
        }

        public IEnumerable<FishingContent> Reload()
        {
            // Load content packs
            foreach (var pack in this.helper.ContentPacks.GetOwned())
            {
                // Traits
                if (!this.TryRead<FishTraitsPack>(pack, "fishTraits.json", out var fishTraits))
                {
                    continue;
                }

                // Fish entries
                if (!this.TryRead<FishPack>(pack, "fish.json", out var fishEntries))
                {
                    continue;
                }

                // Trash entries
                if (!this.TryRead<TrashPack>(pack, "trash.json", out var trashEntries))
                {
                    continue;
                }

                // Treasure entries
                if (!this.TryRead<TreasurePack>(pack, "treasure.json", out var treasureEntries))
                {
                    continue;
                }

                yield return new(pack.Manifest)
                {
                    FishTraits =
                        fishTraits?.Add ?? ImmutableDictionary<NamespacedKey, FishTraits>.Empty,
                    FishEntries = fishEntries?.Add ?? ImmutableArray<FishEntry>.Empty,
                    TrashEntries = trashEntries?.Add ?? ImmutableArray<TrashEntry>.Empty,
                    TreasureEntries =
                        treasureEntries?.Add ?? ImmutableArray<TreasureEntry>.Empty,
                };
            }
        }

        private bool TryRead<T>(IContentPack pack, string path, out T? result)
            where T : class
        {
            try
            {
                result = this.jsonProvider.ReadJson<T>(
                    path,
                    new ContentPackAssetProvider(pack),
                    null
                );
                return true;
            }
            catch (Exception ex)
            {
                this.monitor.Log(
                    $"Error loading content pack '{pack.Manifest.UniqueID}'. The file {path} is invalid.\n{ex}",
                    LogLevel.Error
                );
                result = default;
                return false;
            }
        }
    }
}