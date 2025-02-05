using Valour.Shared.Models;

namespace Valour.Server.Models;

public class TenorFavorite : Item, ISharedTenorFavorite
{
    public long Id { get; set; }
    
    public long UserId { get; set; }
    
    public string TenorId { get; set; }
}