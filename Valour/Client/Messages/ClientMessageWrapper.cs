﻿using Markdig.Helpers;
using System.Diagnostics;
using System.Text.Json;
using Valour.Api.Models;
using Valour.Client.Components.Messages;
using Valour.Shared.Models;

namespace Valour.Client.Messages;

/*  Valour - A free and secure chat client
*  Copyright (C) 2021 Vooper Media LLC
*  This program is subject to the GNU Affero General Public license
*  A copy of the license should be included - if not, see <http://www.gnu.org/licenses/>
*/

public class ClientMessageWrapper
{
    /// <summary>
    /// True if this message's content has been fully built
    /// </summary>
    private bool _generated = false;

    /// <summary>
    /// True if the markdown has already been generated
    /// </summary>
    private bool _markdownParsed = false;

    /// <summary>
    /// The fragments used for building elements
    /// </summary>
    private List<ElementFragment> _elementFragments;

    /// <summary>
    /// The markdown-parsed version of the Content
    /// </summary>
    private string _markdownContent;

    private string _renderKey;
    
    /// <summary>
    /// Used to prevent key clashes
    /// </summary>
    public string RenderKey
    {
        get
        {
            if (_renderKey is null)
            {
                _renderKey = Guid.NewGuid().ToString();
            }

            return _renderKey;
        }
    }

    /// <summary>
    /// The internal message
    /// </summary>
    public Message Message { get; set; }
    
    /// <summary>
    /// The replied to message
    /// </summary>
    public ClientMessageWrapper Reply { get; set; }

    public static List<ClientMessageWrapper> FromList(List<Message> data)
    {
        List<ClientMessageWrapper> result = new();

        if (data is null)
            return result;

        foreach (var msg in data)
            result.Add(new ClientMessageWrapper(msg));

        return result;
    }

    public ClientMessageWrapper(Message message)
    {
        Message = message;
        var reply = message.GetReply();
        if (reply is not null)
            Reply = new ClientMessageWrapper(reply);
    }

    public string MarkdownContent
    {
        get
        {
            if (!_markdownParsed)
            {
                ParseMarkdown();
            }

            return _markdownContent;
        }
    }

    private void ParseMarkdown()
    {
        _markdownContent = MarkdownManager.GetHtml(Message.Content);
        _markdownParsed = true;
    }

    private static readonly HashSet<string> InlineTags = new()
    {
        "b",
        "/b",
        "em",
        "/em",
        "strong",
        "/strong",
        "blockquote",
        "/blockquote",
        "p",
        "/p",
        "h1",
        "h2",
        "h3",
        "h4",
        "h5",
        "h6",
        "/h1",
        "/h2",
        "/h3",
        "/h4",
        "/h5",
        "/h6",
        "code",
        "/code",
        "br"
    };

    private static readonly HashSet<string> SelfClosingTags = new()
    {
        "br"
    };

    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Message.EmbedData) && string.IsNullOrWhiteSpace(Message.Content) && 
        Message.ReplyToId is null && 
        (Message.Attachments is null || Message.Attachments.Count == 0);

    public void Clear()
    {
        _markdownContent = null;
        _elementFragments = null;
        _generated = false;
        _markdownParsed = false;
    }

    public void GenerateForPost()
    {
        Message.ClearMentions();

        var pos = 0;

        var text = MarkdownContent;

        var planetMessage = Message as PlanetMessage;
        var directMessage = Message as DirectMessage;

        while (pos < text.Length)
        {
            if (text[pos] == '«')
            {
                var remainingLength = text.Length - pos;
                
                // Must be at least this long ( «@x-x» )
                if (remainingLength < 6)
                {
                    pos++;
                    continue;
                }
                // Mentions (<@x-)
                if (text[pos + 1] == '@' &&
                    text[pos + 3] == '-')
                {
                    // Member mention (<@m-)
                    if (planetMessage is not null && text[pos + 2] == 'm')
                    {
                        // Extract id
                        char c = ' ';
                        int offset = 4;
                        string id_chars = "";
                        while (offset < remainingLength &&
                               (c = text[pos + offset]).IsDigit())
                        {
                            id_chars += c;
                            offset++;
                        }
                        // Make sure ending tag is '>'
                        if (c != '»')
                        {
                            pos++;
                            continue;
                        }
                        if (string.IsNullOrWhiteSpace(id_chars))
                        {
                            pos++;
                            continue;
                        }
                        bool parsed = long.TryParse(id_chars, out long id);
                        if (!parsed)
                        {
                            pos++;
                            continue;
                        }
                        // Create object
                        Mention memberMention = new()
                        {
                            TargetId = id,
                            Position = (ushort)pos,
                            Length = (ushort)(6 + id_chars.Length),
                            Type = MentionType.Member,
                            PlanetId = planetMessage.PlanetId                     
                        };
                        
                        Message.SetupMentionsList();
                        Message.Mentions.Add(memberMention);
                    }
                    else if (planetMessage is not null && text[pos + 2] == 'r')
                    {
                        // Extract id
                        char c = ' ';
                        int offset = 4;
                        string id_chars = "";
                        while (offset < remainingLength &&
                               (c = text[pos + offset]).IsDigit())
                        {
                            id_chars += c;
                            offset++;
                        }
                        // Make sure ending tag is '>'
                        if (c != '»')
                        {
                            pos++;
                            continue;
                        }
                        if (string.IsNullOrWhiteSpace(id_chars))
                        {
                            pos++;
                            continue;
                        }
                        bool parsed = long.TryParse(id_chars, out long id);
                        if (!parsed)
                        {
                            pos++;
                            continue;
                        }
                        // Create object
                        Mention roleMention = new()
                        {
                            TargetId = id,
                            Position = (ushort)pos,
                            Length = (ushort)(6 + id_chars.Length),
                            Type = MentionType.Role,
                            PlanetId = planetMessage.PlanetId
                        };

                        Message.SetupMentionsList();
                        Message.Mentions.Add(roleMention);
                    }
                    // Other mentions go here
                    else if (directMessage is not null && text[pos + 2] == 'u')
                    {
                        // Extract id
                        char c = ' ';
                        int offset = 4;
                        string id_chars = "";
                        while (offset < remainingLength &&
                               (c = text[pos + offset]).IsDigit())
                        {
                            id_chars += c;
                            offset++;
                        }
                        // Make sure ending tag is '>'
                        if (c != '»')
                        {
                            pos++;
                            continue;
                        }
                        if (string.IsNullOrWhiteSpace(id_chars))
                        {
                            pos++;
                            continue;
                        }
                        bool parsed = long.TryParse(id_chars, out long id);
                        if (!parsed)
                        {
                            pos++;
                            continue;
                        }
                        // Create object
                        Mention userMention = new()
                        {
                            TargetId = id,
                            Position = (ushort)pos,
                            Length = (ushort)(6 + id_chars.Length),
                            Type = MentionType.User,
                        };

                        Message.SetupMentionsList();
                        Message.Mentions.Add(userMention);
                    }
                    else
                    {
                        pos++;
                        continue;
                    }
                }
                // Channel mentions (<#x-)
                if (text[pos + 1] == '#' &&
                    text[pos + 3] == '-')
                {
                    // Chat Channel mention (<#c-)
                    if (planetMessage is not null && text[pos + 2] == 'c')
                    {
                        // Extract id
                        char c = ' ';
                        int offset = 4;
                        string id_chars = "";
                        while (offset < remainingLength &&
                               (c = text[pos + offset]).IsDigit())
                        {
                            id_chars += c;
                            offset++;
                        }
                        // Make sure ending tag is '>'
                        if (c != '»')
                        {
                            pos++;
                            continue;
                        }
                        if (string.IsNullOrWhiteSpace(id_chars))
                        {
                            pos++;
                            continue;
                        }
                        bool parsed = long.TryParse(id_chars, out long id);
                        if (!parsed)
                        {
                            pos++;
                            continue;
                        }
                        // Create object
                        Mention channelMention = new()
                        {
                            TargetId = id,
                            Position = (ushort)pos,
                            Length = (ushort)(6 + id_chars.Length),
                            Type = MentionType.Channel,
                            PlanetId = planetMessage.PlanetId
                        };

                        Message.SetupMentionsList();
                        Message.Mentions.Add(channelMention);
                    }
                    else
                    {
                        pos++;
                        continue;
                    }
                }

                // Put future things here
                else
                {
                    pos++;
                    continue;
                }
            }
            // Stock ticker handler
            else if (text[pos] == '$')
            {
                var remainingLength = text.Length - pos;
                if (remainingLength == 0 || !char.IsLetter(text[pos + 1])
                    )
                {
                    pos++;
                    continue;
                }

                string ticker = "";

                var i = 1;
                
                // Extract ticker
                // Ensure that character is a letter
                while (i < remainingLength && char.IsLetter(text[pos + i]))
                {
                    ticker += text[pos + i];
                    i++;

                    if (i > 6)
                    {
                        break;
                    }
                }
                
                var stock = new Mention()
                {
                    Type = MentionType.Stock,
                    Data = ticker,
                    Position = (ushort)pos,
                    Length = (ushort)(2 + ticker.Length)
                };
                
                Message.SetupMentionsList();
                Message.Mentions.Add(stock);
            }

            pos++;
        }

        Message.MentionsData = JsonSerializer.Serialize(Message.Mentions);

        Message.AttachmentsData = JsonSerializer.Serialize(Message.Attachments);
    }

    /// <summary>
    /// Parses (should parse AFTER markdown is generated!)
    /// </summary>
    public void GenerateMessage()
    {
        if (_elementFragments == null)
        {
            _elementFragments = new List<ElementFragment>(2);
        }
        else
        {
            _elementFragments.Clear();
        }

        int pos = 0;

        string text = MarkdownContent;

        // Scan over full text
        while (pos < text.Length)
        {

            // Custom support for markdown things that are broken horribly.
            // Detect html tags and build fragments
            if (text[pos] == '<')
            {
                // A pure '<' can only be generated from markup, meaning we should
                // always be able to find an end tag. I think.

                int offset = pos + 1;
                string tag = "";

                while (text[offset] != '>')
                {
                    tag += text[offset];
                    offset++;
                }

                if (InlineTags.Contains(tag))
                {
                    // Allow these tags but be careful and block
                    // any we don't intend to be handled this way
                }
                else
                {
                    pos++;
                    continue;
                }

                // We should now have the full tag

                // Check if this is a closing tag

                // Closing
                if (tag[0] == '/')
                {
                    ElementFragment end = new()
                    {
                        Closing = true,
                        Attributes = null,
                        Length = (ushort)(2 + (offset - pos)),
                        Position = (ushort)pos,
                        Tag = tag
                    };

                    _elementFragments.Add(end);
                }
                // Opening
                else
                {
                    ElementFragment start = new()
                    {
                        Closing = false,
                        Attributes = null,
                        Length = (ushort)(2 + (offset - pos)),
                        Position = (ushort)pos,
                        Tag = tag
                    };

                    if (SelfClosingTags.Contains(tag))
                    {
                        start.Self_Closing = true;
                    }

                    _elementFragments.Add(start);
                }
            }

            pos++;

        }

        _generated = true;
    }

    /// <summary>
    /// Returns the message fragments used to render this message
    /// </summary>
    public List<MessageFragment> GetMessageFragments()
    {
        // Stopwatch sw = new();

        if (!_generated)
        {
            GenerateMessage();
        }

        // sw.Start();

        List<MessageFragment> fragments = new();

        // Empty message catch
        if (string.IsNullOrWhiteSpace(Message.Content))
        {
            return fragments;
        }

        // First insert rich components into list and sort by position
        if (Message.Mentions != null && Message.Mentions.Count > 0)
        {
            foreach (var mention in Message.Mentions)
            {
                MessageFragment fragment = null;

                switch (mention.Type)
                {
                    case MentionType.Member:
                    {
                        fragment = new MemberMentionFragment()
                        {
                            Mention = mention,
                            Position = mention.Position,
                            Length = mention.Length
                        };
                        break;
                    }
                    case MentionType.User:
                    {
                        fragment = new UserMentionFragment()
                        {
                            Mention = mention,
                            Position = mention.Position,
                            Length = mention.Length
                        };
                        break;
                    }
                    case MentionType.Channel:
                    {
                        fragment = new ChannelMentionFragment()
                        {
                            Mention = mention,
                            Position = mention.Position,
                            Length = mention.Length
                        };
                        break;
                    }
                    case MentionType.Role:
                    {
                        fragment = new RoleMentionFragment()
                        {
                            Mention = mention,
                            Position = mention.Position,
                            Length = mention.Length
                        };
                        break;
                    }
                    case MentionType.Stock:
                    {
                        fragment = new StockMentionFragment()
                        {
                            Mention = mention,
                            Position = mention.Position,
                            Length = mention.Length
                        };
                        break;
                    }
                }

                if (fragment != null)
                {
                    fragments.Add(fragment);
                }
            }
        }

        // Shortcut if there is no rich content
        if (fragments.Count == 0)
        {
            MarkdownFragment fragment = new()
            {
                Content = MarkdownContent,
                Position = 0,
                Length = (ushort)MarkdownContent.Length
            };

            fragments.Add(fragment);

            return fragments;
        }

        // Add fancy inline-supported fragments
        fragments.AddRange(_elementFragments);

        // Sort rich components
        var orderedRich = fragments.OrderBy(x => x.Position);
        var finFragments = orderedRich.ToList();

        ushort start = 0;
        // Now split the content at each rich starting position
        // and insert the first half into the fragments
        foreach (var rich in orderedRich)
        {
            if (rich.Position != 0)
            {
                string markContent = MarkdownContent[start..rich.Position];

                MarkdownFragment fragment = new()
                {
                    Content = markContent,
                    Position = start,
                    Length = (ushort)markContent.Length
                };

                int index = finFragments.IndexOf(rich);

                // Insert right before rich element
                finFragments.Insert(index, fragment);
            }

            start = (ushort)(rich.Position + rich.Length - 1);
        }

        string endContent = MarkdownContent[start..];

        // There will be one remaining fragment for whatever is left
        MarkdownFragment end = new()
        {
            Content = endContent,
            Position = start,
            Length = (ushort)endContent.Length
        };

        finFragments.Add(end);

        // sw.Stop();

        // Console.WriteLine("Elapsed: " + sw.Elapsed.Milliseconds);

        return finFragments;
    }
}
