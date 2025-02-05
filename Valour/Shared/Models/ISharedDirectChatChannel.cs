﻿using Valour.Shared.Models;

namespace Valour.Shared.Models;

public interface ISharedDirectChatChannel  : ISharedChatChannel
{
    /// <summary>
    /// One of the users in the DM channel
    /// </summary>
    long UserOneId { get; set; }

    /// <summary>
    /// One of the users in the DM channel
    /// </summary>
    long UserTwoId { get; set; }
}
