﻿using Microsoft.AspNetCore.Components.Rendering;
using Valour.Client.Components.Messages;
using Valour.Shared.Models;

namespace Valour.Client.Messages;

/*  Valour - A free and secure chat client
 *  Copyright (C) 2021 Vooper Media LLC
 *  This program is subject to the GNU Affero General Public license
 *  A copy of the license should be included - if not, see <http://www.gnu.org/licenses/>
 */

public class UserMentionFragment : MessageFragment
{
    public Mention Mention { get; set; }

    public override void BuildRenderTree(RenderTreeBuilder builder, ref int stage)
    {
        builder.OpenComponent<UserMentionComponent>(stage);
        stage++;
        builder.AddAttribute(stage, "Mention", Mention);
        stage++;
        builder.CloseComponent();
    }
}

