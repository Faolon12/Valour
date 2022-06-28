﻿using System.ComponentModel.DataAnnotations;

/*  Valour - A free and secure chat client
 *  Copyright (C) 2021 Vooper Media LLC
 *  This program is subject to the GNU Affero General Public license
 *  A copy of the license should be included - if not, see <http://www.gnu.org/licenses/>
 */

namespace Valour.Server.Database.Items.Users;

/// <summary>
/// Assists in password recovery systems
/// </summary>
public class PasswordRecovery
{
    [Key]
    public string Code { get; set; }
    public ulong UserId { get; set; }

    [ForeignKey("UserId")]
    public virtual User User { get; set; }
}

