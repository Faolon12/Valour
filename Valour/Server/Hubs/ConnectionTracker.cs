using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
using Valour.Server.API;
using Valour.Server.Config;
using Valour.Server.Database;
using Valour.Server.Database.Items.Authorization;
using Valour.Server.Database.Nodes;
using Valour.Server.Redis;
using Valour.Shared;

namespace Valour.Server.Hubs;

public class ConnectionTracker
{
    /// <summary>
    /// We can store the authentication tokens for clients after they connect to the hub, and use them as long as the connection lasts
    /// </summary>
    public static readonly ConcurrentDictionary<string, AuthToken> ConnectionIdentities = new();

    // Map of groups to joined identities 
    public static readonly ConcurrentDictionary<string, List<string>> GroupConnections = new();

    // Map of groups to user ids
    public static readonly ConcurrentDictionary<string, List<long>> GroupUserIds = new();

    // Map of connection to joined groups
    public static readonly ConcurrentDictionary<string, List<string>> ConnectionGroups = new();

    // Map of user id to joined groups
    public static readonly ConcurrentDictionary<long, List<string>> UserIdGroups = new();

    // Map of connection id to user id
    // This specifically stores primary user connections
    public static readonly ConcurrentDictionary<string, PrimaryNodeConnection> PrimaryConnections = new();

    public static AuthToken GetToken(string connectionId)
    {
        if (!ConnectionIdentities.ContainsKey(connectionId))
            return null;

        return ConnectionIdentities[connectionId];
    }
    
    public static void TrackGroupMembership(string groupId, HubCallerContext context)
    {
        // Create connection group list if it doesn't exist
        if (!ConnectionGroups.ContainsKey(context.ConnectionId))
            ConnectionGroups[context.ConnectionId] = new();

        // Add group to connection
        ConnectionGroups[context.ConnectionId].Add(groupId);

        // Create group connection list if it doesn't exist
        if (!GroupConnections.ContainsKey(groupId))
            GroupConnections[groupId] = new();

        // Add connection to group
        GroupConnections[groupId].Add(context.ConnectionId);

        // User part

        // Get identity of the connection
        ConnectionIdentities.TryGetValue(context.ConnectionId, out var token);
        if (token is null)
            return;

        var userId = token.UserId;

        // Create user group list if it doesn't exist
        if (!UserIdGroups.ContainsKey(userId))
            UserIdGroups[userId] = new();

        // Add group to user
        // if (!UserIdGroups[userId].Contains(groupId))
            UserIdGroups[userId].Add(groupId);

        // Create group user list if it doesn't exist
        if (!GroupUserIds.ContainsKey(groupId))
            GroupUserIds[groupId] = new();

        // Add user to group if not already
        //if (!GroupUserIds[groupId].Contains(userId)) This is actually bad
            GroupUserIds[groupId].Add(userId);
    }

    public static void UntrackGroupMembership(string groupId, HubCallerContext context)
    {
        // Remove connection from group
        GroupConnections.TryGetValue(groupId, out var connections);
        if (connections is not null)
            connections.Remove(context.ConnectionId);

        // Remove group from connection
        ConnectionGroups.TryGetValue(context.ConnectionId, out var groups);
        if (groups is not null)
            groups.Remove(groupId);

        // Get connection identity
        ConnectionIdentities.TryGetValue(context.ConnectionId, out var authToken);
        if (authToken is null)
            return;

        var userId = authToken.UserId;

        // Remove userid from group
        GroupUserIds.TryGetValue(groupId, out var userIds);
        if (userIds is not null)
            userIds.Remove(userId);

        // Remove group id from user
        UserIdGroups.TryGetValue(userId, out var groupIds);
        if (groupIds is not null)
            groupIds.Remove(groupId);
    }

    public static void RemoveAllMemberships(HubCallerContext context)
    {
        // Get all groups for connection
        ConnectionGroups.TryGetValue(context.ConnectionId, out var groups);
        if (groups is null)
            return;

        // Clear each group
        foreach (var group in groups.ToArray())
            UntrackGroupMembership(group, context);

        // Remove connection key from groups
        ConnectionGroups.Remove(context.ConnectionId, out _);

        // Remove connection identity
        //ConnectionIdentities.Remove(Context.ConnectionId, out _);
    }

    public static async Task AddPrimaryConnection(long userId, HubCallerContext context, IConnectionMultiplexer redis)
    {
        var conn = new PrimaryNodeConnection()
        {
            ConnectionId = context.ConnectionId,
            UserId = userId,
            OpenTime = DateTime.UtcNow,
            NodeId = NodeAPI.Node.Name
        };
        
        // Add to collection
        PrimaryConnections.TryAdd(context.ConnectionId, conn);
        
        var rdb = redis.GetDatabase(RedisDbTypes.Connections);

        // Connection to node
        rdb.SetAdd($"node:{NodeAPI.Node.Name}", $"{userId}:{context.ConnectionId}");
        
        // Specific connection by user
        rdb.SetAdd($"user:{userId}", $"{NodeConfig.Instance.Name}:{context.ConnectionId}");
    }

    public static async Task RemovePrimaryConnection(HubCallerContext context, IConnectionMultiplexer redis)
    {
        // Remove any existing connection from collection
        PrimaryConnections.Remove(context.ConnectionId, out _);
        
        var userId = ConnectionIdentities[context.ConnectionId].UserId;
        
        var rdb = redis.GetDatabase(RedisDbTypes.Connections);
        
        // Connection to node
        rdb.SetRemove($"node:{NodeAPI.Node.Name}", $"{userId}:{context.ConnectionId}");
        
        // Specific connection by user
        rdb.SetRemove($"user:{userId}", $"{NodeConfig.Instance.Name}:{context.ConnectionId}");
    }
}