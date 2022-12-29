﻿using Valour.Shared.Models;

namespace Valour.Api.Items.Channels;
public interface IChannel : ISharedItem
{
    public string Name { get; set; }
    public string Description { get; set; }
    public Task Open();
    public Task Close();
}
