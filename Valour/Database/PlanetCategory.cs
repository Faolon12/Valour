﻿using System.ComponentModel.DataAnnotations.Schema;
using Valour.Shared.Models;
using Valour.Shared.Models;

namespace Valour.Database;

[Table("planet_category_channels")]
public class PlanetCategory : PlanetChannel, ISharedPlanetCategory
{
    ///////////////////////
    // Entity Properties //
    ///////////////////////
    
    public override ChannelType Type => ChannelType.PlanetCategoryChannel;
}

