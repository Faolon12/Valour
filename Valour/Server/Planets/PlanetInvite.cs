using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Valour.Server.Database;
using Valour.Shared.Planets;
using Valour.Shared.Users;

namespace Valour.Server.Planets
{
    /*  Valour - A free and secure chat client
     *  Copyright (C) 2020 Vooper Media LLC
     *  This program is subject to the GNU Affero General Public license
     *  A copy of the license should be included - if not, see <http://www.gnu.org/licenses/>
     */


    /// <summary>
    /// This represents a user within a planet and is used to represent membership
    /// </summary>
    public class PlanetInvite
    {
        /// <summary>
        /// The Id of this object
        /// </summary>
        [Key]
        public ulong Id { get; set; }

        /// <summary>
        /// the invite code
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// The planet the invite is for
        /// </summary>
        public ulong Planet_Id { get; set; }

        /// <summary>
        /// The user that created the invite
        /// </summary>
        public ulong Issuer_Id { get; set; }

        /// <summary>
        /// The time the invite was created
        /// </summary>
        public DateTime Time { get; set; }

        /// <summary>
        /// The length of the invite before its invaild
        /// </summary>
        public uint? Hours { get; set; }

        public bool IsPermanent()
        {
            return (Hours == null);
        }

        public string GetCode() {
            return RandomString(9);
        }

        private static Random random = new Random();
        public static string RandomString(int length)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}
