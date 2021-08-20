﻿using System.Text.Json;
using System.Text.Json.Serialization;

namespace Valour.Shared.Items
{
    /// <summary>
    /// Enum for all Valour item types
    /// </summary>
    public enum ItemType
    {
        Channel,
        Category,
        Planet
    }

    /// <summary>
    /// Common interface for Valour API items
    /// </summary>
    public interface IItem
    {
        [JsonInclude]
        public ulong Id { get; set; }

        [JsonInclude]
        public ItemType ItemType { get; }
    }
}
