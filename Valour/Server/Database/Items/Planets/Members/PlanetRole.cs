﻿using Microsoft.AspNetCore.Mvc;
using System.Drawing;
using Valour.Server.Database.Items.Authorization;
using Valour.Server.Database.Items.Planets.Channels;
using Valour.Shared.Authorization;
using Valour.Shared.Http;
using Valour.Shared.Items;
using Valour.Shared.Items.Planets.Members;

/*  Valour - A free and secure chat client
 *  Copyright (C) 2021 Vooper Media LLC
 *  This program is subject to the GNU Affero General Public license
 *  A copy of the license should be included - if not, see <http://www.gnu.org/licenses/>
 */


namespace Valour.Server.Database.Items.Planets.Members;

public class PlanetRole : PlanetItem, ISharedPlanetRole
{
    [InverseProperty("Role")]
    [JsonIgnore]
    public virtual ICollection<PermissionsNode> PermissionNodes { get; set; }

    /// <summary>
    /// The position of the role: Lower has more authority
    /// </summary>
    public uint Position { get; set; }

    /// <summary>
    /// The planet permissions for the role
    /// </summary>
    public ulong Permissions { get; set; }

    // RGB Components for role color
    public byte Red { get; set; }
    public byte Green { get; set; }
    public byte Blue { get; set; }

    // Formatting options
    public bool Bold { get; set; }
    public bool Italics { get; set; }

    public string Name { get; set; }

    public override ItemType ItemType => ItemType.PlanetRole;

    public uint GetAuthority() =>
        ISharedPlanetRole.GetAuthority(this);

    public Color GetColor() =>
        ISharedPlanetRole.GetColor(this);

    public string GetColorHex() =>
        ISharedPlanetRole.GetColorHex(this);

    public bool HasPermission(PlanetPermission perm) =>
        ISharedPlanetRole.HasPermission(this, perm);

    public ICollection<PermissionsNode> GetNodes(ValourDB db)
    {
        PermissionNodes ??= db.PermissionsNodes.Where(x => x.RoleId == Id).ToList();
        return PermissionNodes;
    }

    public ICollection<PermissionsNode> GetChannelNodes(ValourDB db)
    {
        PermissionNodes ??= db.PermissionsNodes.Where(x => x.RoleId == Id).ToList();
        return PermissionNodes.Where(x => x.TargetType == ItemType.PlanetChatChannel).ToList();
    }

    public ICollection<PermissionsNode> GetCategoryNodes(ValourDB db)
    {
        PermissionNodes ??= db.PermissionsNodes.Where(x => x.RoleId == Id).ToList();
        return PermissionNodes.Where(x => x.TargetType == ItemType.PlanetCategoryChannel).ToList();
    }

    public async Task<PermissionsNode> GetChannelNodeAsync(PlanetChatChannel channel, ValourDB db) =>
        await db.PermissionsNodes.FirstOrDefaultAsync(x => x.TargetId == channel.Id &&
                                                                     x.TargetType == ItemType.PlanetChatChannel);

    public async Task<PermissionsNode> GetChannelNodeAsync(PlanetCategoryChannel category, ValourDB db) =>
        await db.PermissionsNodes.FirstOrDefaultAsync(x => x.TargetId == category.Id &&
                                                                     x.TargetType == ItemType.PlanetChatChannel);

    public async Task<PermissionsNode> GetCategoryNodeAsync(PlanetCategoryChannel category, ValourDB db) =>
        await db.PermissionsNodes.FirstOrDefaultAsync(x => x.TargetId == category.Id &&
                                                                     x.TargetType == ItemType.PlanetCategoryChannel);

    public async Task<PermissionState> GetPermissionStateAsync(Permission permission, PlanetChatChannel channel, ValourDB db) =>
        await GetPermissionStateAsync(permission, channel.Id, db);

    public async Task<PermissionState> GetPermissionStateAsync(Permission permission, ulong channelId, ValourDB db) =>
        (await db.PermissionsNodes.FirstOrDefaultAsync(x => x.RoleId == Id && x.TargetId == channelId)).GetPermissionState(permission);

    public async Task DeleteAsync(ValourDB db)
    {
        // Remove all members
        var members = db.PlanetRoleMembers.Where(x => x.RoleId == Id);
        db.PlanetRoleMembers.RemoveRange(members);

        // Remove role nodes
        var nodes = GetNodes(db);

        db.PermissionsNodes.RemoveRange(nodes);

        // Remove self
        db.PlanetRoles.Remove(this);
    }

    [ValourRoute(HttpVerbs.Get), TokenRequired, InjectDb]
    [UserPermissionsRequired(UserPermissionsEnum.Membership)]
    [PlanetMembershipRequired]
    public static async Task<IResult> GetRouteAsync(HttpContext ctx, ulong id)
    {
        var db = ctx.GetDb();

        var role = await FindAsync<PlanetRole>(id, db);

        if (role is null)
            return ValourResult.NotFound<PlanetRole>();

        return Results.Json(role);
    }

    [ValourRoute(HttpVerbs.Post), TokenRequired, InjectDb]
    [UserPermissionsRequired(UserPermissionsEnum.PlanetManagement)]
    [PlanetMembershipRequired]
    [PlanetPermsRequired(PlanetPermissionsEnum.ManageRoles)]
    public static async Task<IResult> PostRouteAsync(HttpContext ctx, [FromBody] PlanetRole role,
        ILogger<PlanetRole> logger)
    {
        var db = ctx.GetDb();
        var authMember = ctx.GetMember();

        role.Position = (uint)await db.PlanetRoles.CountAsync(x => x.PlanetId == role.PlanetId);
        role.Id = IdManager.Generate();

        if (role.GetAuthority() > await authMember.GetAuthorityAsync(db))
            return ValourResult.Forbid("You cannot create roles with higher authority than your own.");

        try
        {
            await db.AddAsync(role);
            await db.SaveChangesAsync();
        }
        catch (System.Exception e)
        {
            logger.LogError(e.Message);
            return Results.Problem(e.Message);
        }

        PlanetHub.NotifyPlanetItemChange(role);

        return Results.Created(role.GetUri(), role);

    }

    [ValourRoute(HttpVerbs.Put), TokenRequired, InjectDb]
    [UserPermissionsRequired(UserPermissionsEnum.PlanetManagement)]
    [PlanetMembershipRequired]
    [PlanetPermsRequired(PlanetPermissionsEnum.ManageRoles)]
    public static async Task<IResult> PutRouteAsync(HttpContext ctx, ulong id, [FromBody] PlanetRole role,
        ILogger<PlanetRole> logger)
    {
        var db = ctx.GetDb();

        var oldRole = await FindAsync<PlanetRole>(id, db);

        if (role.PlanetId != oldRole.PlanetId)
            return Results.BadRequest("You cannot change what planet.");

        if (role.Position != oldRole.Position)
            return Results.BadRequest("Position cannot be changed directly.");

        try
        {
            db.PlanetRoles.Update(role);
            await db.SaveChangesAsync();
        }
        catch (System.Exception e)
        {
            logger.LogError(e.Message);
            return Results.Problem(e.Message);
        }

        PlanetHub.NotifyPlanetItemChange(role);

        return Results.Json(role);

    }

    [ValourRoute(HttpVerbs.Delete), TokenRequired, InjectDb]
    [UserPermissionsRequired(UserPermissionsEnum.PlanetManagement)]
    [PlanetMembershipRequired]
    [PlanetPermsRequired(PlanetPermissionsEnum.ManageRoles)]
    public static async Task<IResult> DeleteRouteAsync(HttpContext ctx, ulong id,
        ILogger<PlanetRole> logger)
    {
        var db = ctx.GetDb();

        var role = await FindAsync<PlanetRole>(id, db);

        try
        {
            await role.DeleteAsync(db);
            await db.SaveChangesAsync();
        }
        catch (System.Exception e)
        {
            logger.LogError(e.Message);
            return Results.Problem(e.Message);
        }

        PlanetHub.NotifyPlanetItemDelete(role);

        return Results.NoContent();

    }
}