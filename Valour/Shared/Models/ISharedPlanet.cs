﻿/*  Valour - A free and secure chat client
 *  Copyright (C) 2021 Vooper Media LLC
 *  This program is subject to the GNU Affero General Public license
 *  A copy of the license should be included - if not, see <http://www.gnu.org/licenses/>
 */

namespace Valour.Shared.Models;

public interface ISharedPlanet : ISharedItem
{
    /// <summary>
    /// The Id of Valour Central, used for some platform-wide features
    /// </summary>
    const long ValourCentralId = 12215159187308544;

    /// <summary>
    /// The Id of the owner of this planet
    /// </summary>
    long OwnerId { get; set; }
    
    /// <summary>
    /// The node this planet belongs to
    /// </summary>
    string NodeName { get; set; } 

    /// <summary>
    /// The name of this planet
    /// </summary>
    string Name { get; set; }

    /// <summary>
    /// The image url for the planet 
    /// </summary>
    string IconUrl { get; set; }

    /// <summary>
    /// The description of the planet
    /// </summary>
    string Description { get; set; }

    /// <summary>
    /// If the server requires express allowal to join a planet
    /// </summary>
    bool Public { get; set; }

    /// <summary>
    /// If this and public are true, a planet will appear on the discovery tab
    /// </summary>
    bool Discoverable { get; set; }
    
    /// <summary>
    /// True if you probably shouldn't be on this server at work owo
    /// </summary>
    bool Nsfw { get; set; }
}

