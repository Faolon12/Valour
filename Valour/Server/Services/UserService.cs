using IdGen;
using Newtonsoft.Json.Linq;
using SendGrid;
using Valour.Database.Context;
using Valour.Server.Database;
using Valour.Server.Email;
using Valour.Server.Users;
using Valour.Shared;
using Valour.Shared.Authorization;
using Valour.Shared.Models;

namespace Valour.Server.Services;

public class UserService
{
    private readonly ValourDB _db;
    private readonly TokenService _tokenService;
    private readonly ILogger<UserService> _logger;
    private readonly CoreHubService _coreHub;
    private readonly NodeService _nodeService;


    /// <summary>
    /// The stored user for the current request
    /// </summary>
    private User _currentUser;

    public UserService(
        ValourDB db,
        TokenService tokenService,
        ILogger<UserService> logger,
        CoreHubService coreHub,
        NodeService nodeService)
    {
        _db = db;
        _tokenService = tokenService;
        _logger = logger;
        _coreHub = coreHub;
        _nodeService = nodeService;
    }

    /// <summary>
    /// Returns the user for the given id
    /// </summary>
    public async Task<User> GetAsync(long id) =>
        (await _db.Users.FindAsync(id)).ToModel();

    public async Task<EmailConfirmCode> GetEmailConfirmCode(string code) =>
        (await _db.EmailConfirmCodes.FirstOrDefaultAsync(x => x.Code == code)).ToModel();

    public async Task<List<Planet>> GetPlanetsUserIn(long userId)
    {
        var planets = await _db.PlanetMembers
            .Where(x => x.UserId == userId)
            .Include(x => x.Planet)
            .Select(x => x.Planet.ToModel())
            .ToListAsync();

        foreach (var planet in planets)
        {
            planet.NodeName = await _nodeService.GetPlanetNodeAsync(planet.Id);
        }

        return planets;
    }

    public async Task<PasswordRecovery> GetPasswordRecoveryAsync(string code) =>
        (await _db.PasswordRecoveries.FirstOrDefaultAsync(x => x.Code == code)).ToModel();

    public async Task<Valour.Database.Credential> GetCredentialAsync(long userId) =>
        await _db.Credentials.FirstOrDefaultAsync(x => x.UserId == userId);

    public async Task<List<UserChannelState>> GetUserChannelStatesAsync(long userId) =>
        await _db.UserChannelStates.Where(x => x.UserId == userId).Select(x => x.ToModel()).ToListAsync();

    public async Task<List<TenorFavorite>> GetTenorFavoritesAsync(long userId) =>
        await _db.TenorFavorites.Where(x => x.UserId == userId).Select(x => x.ToModel()).ToListAsync();

    public async Task<(List<User> added, List<User> addedBy)> GetFriendsDataAsync(long userId)
    {
        // Users added by this user as a friend (user -> other)
        var added = await _db.UserFriends.Include(x => x.Friend).Where(x => x.UserId == userId).Select(x => x.Friend.ToModel()).ToListAsync();

        // Users who added this user as a friend (other -> user)
        var addedBy = await _db.UserFriends.Include(x => x.User).Where(x => x.FriendId == userId).Select(x => x.User.ToModel()).ToListAsync();

        return (added, addedBy);
    }

    public async Task<List<User>> GetFriends(long userId)
    {
        // Users added by this user as a friend (user -> other)
        var added = _db.UserFriends.Where(x => x.UserId == userId);

        // Users who added this user as a friend (other -> user)
        var addedBy = _db.UserFriends.Where(x => x.FriendId == userId);

        // Mutual friendships
        var mutual = added.Select(x => x.FriendId).Intersect(addedBy.Select(x => x.UserId));

        var friends = await _db.Users.Where(x => mutual.Contains(x.Id)).Select(x => x.ToModel()).ToListAsync();

        return friends;
    }

    public async Task<UserPrivateInfo> GetUserEmailAsync(string email, bool makelowercase = true)
    {
        if (!makelowercase)
            return (await _db.UserEmails.FindAsync(email)).ToModel();
        else
            return (await _db.UserEmails.FirstOrDefaultAsync(x => x.Email.ToLower() == email.ToLower())).ToModel();
    }

    public async Task<TaskResult> SendPasswordResetEmail(UserPrivateInfo userPrivateInfo, string email, HttpContext ctx)
    {
        try
        {
            var oldRecoveries = _db.PasswordRecoveries.Where(x => x.UserId == userPrivateInfo.UserId);
            if (oldRecoveries.Any())
            {
                _db.PasswordRecoveries.RemoveRange(oldRecoveries);
                await _db.SaveChangesAsync();
            }

            string recoveryCode = Guid.NewGuid().ToString();

            PasswordRecovery recovery = new()
            {
                Code = recoveryCode,
                UserId = userPrivateInfo.UserId
            };

            await _db.PasswordRecoveries.AddAsync(recovery.ToDatabase());
            await _db.SaveChangesAsync();

            var host = ctx.Request.Host.ToUriComponent();
            string link = $"{ctx.Request.Scheme}://{host}/RecoverPassword/{recoveryCode}";

            string emsg = $@"<body>
                              <h2 style='font-family:Helvetica;'>
                                Valour Password Recovery
                              </h2>
                              <p style='font-family:Helvetica;>
                                If you did not request this email, please ignore it.
                                To reset your password, please use the following link: 
                              </p>
                              <p style='font-family:Helvetica;'>
                                <a href='{link}'>Click here to recover</a>
                              </p>
                            </body>";

            string rawmsg = $"To reset your password, please go to the following link:\n{link}";

            var result = await EmailManager.SendEmailAsync(email, "Valour Password Recovery", rawmsg, emsg);

            if (!result.IsSuccessStatusCode)
            {
                _logger.LogError($"Error issuing password reset email to {email}. Status code {result.StatusCode}.");
                return new(false, "Sorry! There was an issue sending the email. Try again?");
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
            return new(false, "Sorry! An unexpected error occured. Try again?");
        }

        return new(true, "Success");
    }

    public async Task<TaskResult> RecoveryUserAsync(PasswordRecoveryRequest request, PasswordRecovery recovery, Valour.Database.Credential cred)
    {
        using var tran = await _db.Database.BeginTransactionAsync();

        try
        {
            _db.PasswordRecoveries.Remove(await _db.PasswordRecoveries.FindAsync(recovery.Code));

            byte[] salt = PasswordManager.GenerateSalt();
            byte[] hash = PasswordManager.GetHashForPassword(request.Password, salt);

            cred.Salt = salt;
            cred.Secret = hash;

            _db.Credentials.Update(cred);
            await _db.SaveChangesAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
            return new(false, "We're sorry. Something unexpected occured. Try again?");
        }

        await tran.CommitAsync();

        return new(true, "Success");
    }

    public int GetYearsOld(DateTime birthDate)
    {
        var now = DateTime.Today;
        var age = now.Year - birthDate.Year;
        if (birthDate > now.AddYears(-age)) age--;

        return age;
    }

    public async Task<TaskResult> SetUserComplianceData(long userId, DateTime birthDate, Locality locality)
    {
        if (GetYearsOld(birthDate) < 13)
            return new TaskResult(false, "You must be 13 or older to use Valour. Sorry!");

        birthDate = DateTime.SpecifyKind(birthDate, DateTimeKind.Utc);
        
        var user = await _db.Users.FindAsync(userId);
        if (user is null)
            return new TaskResult(false, "User not found");
        
        var userPrivateInfo = await _db.UserEmails.FirstOrDefaultAsync(x => x.UserId == userId);
        if (userPrivateInfo is null)
            return new TaskResult(false, "User info not found");

        await using var trans = await _db.Database.BeginTransactionAsync();

        try
        {
            userPrivateInfo.BirthDate = birthDate;
            userPrivateInfo.Locality = locality;

            await _db.SaveChangesAsync();

            user.Compliance = true;

            await _db.SaveChangesAsync();

            await trans.CommitAsync();
        }
        catch (Exception e)
        {
            await trans.RollbackAsync();
            return new TaskResult(false, "An unexpected error occured. Try again?");
        }
        
        return TaskResult.SuccessResult;
    }

    public async Task<User> GetUserAsync(string username, string tag)
        => (await _db.Users.FirstOrDefaultAsync(x => x.Name == username.ToLower() && x.Tag == tag.ToUpper())).ToModel();

    public async Task<TaskResult<User>> UpdateAsync(User updatedUser)
    {
        var old = await GetAsync(updatedUser.Id);

        old.Status = updatedUser.Status;

        old.UserStateCode = updatedUser.UserStateCode;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
            return new(false, e.Message);
        }

        await _coreHub.NotifyUserChange(old);

        return new(true, "Success", old);
    }

    public async Task<TaskResult> VerifyAsync(string code)
    {
        await using var tran = await _db.Database.BeginTransactionAsync();
        var confirmCode = await _db.EmailConfirmCodes.FirstOrDefaultAsync(x => x.Code == code);
        if (confirmCode is null)
            return new TaskResult(false, "Code not found.");
        
        try
        {
            var email = await _db.UserEmails.FirstOrDefaultAsync(x => x.UserId == confirmCode.UserId);
            email.Verified = true;
            
            _db.EmailConfirmCodes.Remove(confirmCode);
            await _db.SaveChangesAsync();
            
            await tran.CommitAsync();
        }
        catch (Exception e)
        {
            await tran.RollbackAsync();
            _logger.LogError(e.Message);
            return new(false, e.Message);
        }
        
        return new(true, "Success");
    }

    public async Task<TaskResult> Logout()
    {
        try
        {
            var key = _tokenService.GetAuthKey();
            var dbToken = await _db.AuthTokens.FindAsync(key);
            _db.AuthTokens.Remove(dbToken);
            _tokenService.RemoveFromQuickCache(key);
            await _db.SaveChangesAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
            return new(false, e.Message);
        }

        return new(true, "Success");
    }


    /// <summary>
    /// Returns the user for the current context
    /// </summary>

    public async Task<User> GetCurrentUserAsync()
    {
        var token = await _tokenService.GetCurrentToken();
        if (token is null) return null;
        _currentUser = await GetAsync(token.UserId);
        return _currentUser;
    }
    
    /// <summary>
    /// Returns the user id for the current context
    /// </summary>

    public async Task<long> GetCurrentUserIdAsync()
    {
        var token = await _tokenService.GetCurrentToken();
        return token?.UserId ?? long.MinValue;
    }
    
    /// <summary>
    /// Returns the amount of planets owned by the given user
    /// </summary>
    public Task<int> GetOwnedPlanetCount(long userId) => 
        _db.Planets.CountAsync(x => x.OwnerId == userId);
    
    /// <summary>
    /// Returns the amount of planets joined by the given user
    /// </summary>
    public Task<int> GetJoinedPlanetCount(long userId) => 
        _db.PlanetMembers.CountAsync(x => x.UserId == userId);

    public async Task<TaskResult<User>> ValidateAsync(string credential_type, string identifier, string secret)
    {
        // Find the credential that matches the identifier and type
        Valour.Database.Credential credential = await _db.Credentials.FirstOrDefaultAsync(
            x => string.Equals(credential_type.ToUpper(), x.CredentialType.ToUpper()) &&
                    string.Equals(identifier.ToUpper(), x.Identifier.ToUpper()));

        if (credential == null || string.IsNullOrWhiteSpace(secret))
        {
            return new TaskResult<User>(false, "The credentials were incorrect.", null);
        }

        // Use salt to validate secret hash
        byte[] hash = PasswordManager.GetHashForPassword(secret, credential.Salt);

        // Spike needs to remember how reference types work 
        if (!hash.SequenceEqual(credential.Secret))
        {
            return new TaskResult<User>(false, "The credentials were incorrect.", null);
        }

        User user = await GetAsync(credential.UserId);

        if (user.Disabled)
        {
            return new TaskResult<User>(false, "This account has been disabled", null);
        }

        return new TaskResult<User>(true, "Succeeded", user);
    }

    public async Task<TaskResult<AuthToken>> GetTokenAfterLoginAsync(HttpContext ctx, long userId)
    {
        // Check for an old token
        var token = await _db.AuthTokens
            .FirstOrDefaultAsync(x => x.AppId == "VALOUR" &&
                                      x.UserId == userId &&
                                      x.Scope == UserPermissions.FullControl.Value);

        try
        {
            if (token is null)
            {
                // We now have to create a token for the user
                token = new AuthToken()
                {
                    AppId = "VALOUR",
                    Id = "val-" + Guid.NewGuid().ToString(),
                    TimeCreated = DateTime.UtcNow,
                    TimeExpires = DateTime.UtcNow.AddDays(7),
                    Scope = UserPermissions.FullControl.Value,
                    UserId = userId,
                    IssuedAddress = ctx.Connection.RemoteIpAddress.ToString()
                }.ToDatabase();

                await _db.AuthTokens.AddAsync(token);
                await _db.SaveChangesAsync();
            }
            else
            {
                token.TimeCreated = DateTime.UtcNow;
                token.TimeExpires = DateTime.UtcNow.AddDays(7);

                _db.Entry(token).State = EntityState.Detached;
                _db.AuthTokens.Update(token);
                await _db.SaveChangesAsync();
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
            return new(false, e.Message);
        }

        return new(true, "Success", token.ToModel());
    }

    /// <summary>
    /// Nuke it.
    /// </summary>
    public async Task HardDelete(User user)
    {
        var tran = await _db.Database.BeginTransactionAsync();
        
        var dbUser = await _db.Users.FindAsync(user.Id);
        if (dbUser is null)
            return;

        // Remove messages
        var pMsgs = _db.PlanetMessages.Where(x => x.AuthorUserId == dbUser.Id);
        _db.PlanetMessages.RemoveRange(pMsgs);

        // Direct Message Channels
        var dChannels = await _db.DirectChatChannels
            .Where(x => x.UserOneId == dbUser.Id || x.UserTwoId == dbUser.Id)
            .ToListAsync();

        foreach (var dc in dChannels)
        {
            // channel states
            var st = _db.UserChannelStates.Where(x => x.ChannelId == dc.Id);
            _db.UserChannelStates.RemoveRange(st);

            // messages
            var dMsgs = _db.DirectMessages.Where(x => x.ChannelId == dc.Id);
            _db.DirectMessages.RemoveRange(dMsgs);

            await _db.SaveChangesAsync();
        }

        _db.DirectChatChannels.RemoveRange(dChannels);
        

        // Remove friends and friend requests
        var requests = _db.UserFriends.Where(x => x.UserId == dbUser.Id || x.FriendId == dbUser.Id);
        _db.UserFriends.RemoveRange(requests);

        // Remove email confirm codes
        var codes = _db.EmailConfirmCodes.Where(x => x.UserId == dbUser.Id);
        _db.EmailConfirmCodes.RemoveRange(codes);


        // Remove user emails
        var emails = _db.UserEmails.Where(x => x.UserId == dbUser.Id);
        _db.UserEmails.RemoveRange(emails);

        // Remove credentials
        var creds = _db.Credentials.Where(x => x.UserId == dbUser.Id);
        _db.Credentials.RemoveRange(creds);

        var recovs = _db.PasswordRecoveries.Where(x => x.UserId == dbUser.Id);
        _db.PasswordRecoveries.RemoveRange(recovs);

        // Remove membership stuff
        var pRoles = _db.PlanetRoleMembers.Where(x => x.UserId == dbUser.Id);
        _db.PlanetRoleMembers.RemoveRange(pRoles);

        // Remove planet membership
        var members = _db.PlanetMembers.Where(x => x.UserId == dbUser.Id);
        _db.PlanetMembers.RemoveRange(members);

        await _db.SaveChangesAsync();

        // Authtokens
        var tokens = _db.AuthTokens.Where(x => x.UserId == dbUser.Id);
        _db.AuthTokens.RemoveRange(tokens);

        // Referrals
        var refer = _db.Referrals.Where(x => x.UserId == dbUser.Id || x.ReferrerId == dbUser.Id);
        _db.Referrals.RemoveRange(refer);

        // Notifications
        var nots = _db.NotificationSubscriptions.Where(x => x.UserId == dbUser.Id);

        // Bans
        var bans = _db.PlanetBans.Where(x => x.IssuerId == dbUser.Id || x.TargetId == dbUser.Id);
        _db.PlanetBans.RemoveRange(bans);

        // Channel states
        var states = _db.UserChannelStates.Where(x => x.UserId == dbUser.Id);
        _db.UserChannelStates.RemoveRange(states);

        // Planet invites
        var invites = _db.PlanetInvites.Where(x => x.IssuerId == dbUser.Id);
        _db.PlanetInvites.RemoveRange(invites);

        await _db.SaveChangesAsync();

        _db.Users.Remove(dbUser);
        await _db.SaveChangesAsync();

        try
        {
            await tran.CommitAsync();
            Console.WriteLine("Deleting " + dbUser.Name);
        }
        catch(System.Exception e)
        {
            Console.WriteLine("Error Hard Deleting User!");
            Console.WriteLine(e.Message);
        }
    }

    /// <summary>
    /// Returns the ids for all planet channels the user has access to
    /// </summary>
    public async Task<List<long>> GetAccessiblePlanetChatChannelIdsAsync(long userId)
    {
        var channelIds = await _db.PlanetMembers.Include(x => x.Planet)
            .ThenInclude(x => x.ChatChannels)
            .Where(x => x.UserId == userId)
            .SelectMany(x => x.Planet.ChatChannels.Select(x => x.Id))
            .ToListAsync();
        
        return channelIds;
    }
}