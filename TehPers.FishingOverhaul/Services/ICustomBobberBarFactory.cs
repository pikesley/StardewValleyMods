﻿using StardewValley;
using TehPers.FishingOverhaul.Api;
using TehPers.FishingOverhaul.Api.Content;
using TehPers.FishingOverhaul.Gui;

namespace TehPers.FishingOverhaul.Services
{
    internal interface ICustomBobberBarFactory
    {
        CustomBobberBar? Create(
            FishingInfo fishingInfo,
            FishEntry fishEntry,
            Item fishItem,
            float fishSizePercent,
            bool treasure,
            int bobber,
            bool fromFishPond
        );
    }
}