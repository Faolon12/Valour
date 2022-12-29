﻿namespace Valour.Shared.Models;

public interface ISharedChannel : ISharedItem
{
    DateTime TimeLastActive { get; set; }
    string State { get; set; }
}
