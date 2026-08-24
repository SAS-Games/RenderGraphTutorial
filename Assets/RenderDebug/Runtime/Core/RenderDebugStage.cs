using System;
using System.Collections.Generic;

namespace SAS.RenderDebugging
{
    /// <summary>
    /// Immutable metadata for one ordered rendering stage. It deliberately does not own frame textures.
    /// </summary>
    public readonly struct RenderDebugStage : IEquatable<RenderDebugStage>
    {
        private static readonly RenderDebugChannelInfo[] NoChannels = Array.Empty<RenderDebugChannelInfo>();
        private readonly RenderDebugChannelInfo[] _channels;

        public RenderDebugStage(string id, string displayName, int order,
            RenderDebugStageType type = RenderDebugStageType.Texture, string description = null, string group = null,
            params RenderDebugChannelInfo[] channels)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A debug stage ID cannot be empty.", nameof(id));

            Id = id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName;
            Description = description ?? string.Empty;
            Group = group ?? string.Empty;
            Type = type;
            Order = order;
            _channels = channels == null || channels.Length == 0
                ? NoChannels
                : (RenderDebugChannelInfo[])channels.Clone();
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public string Group { get; }
        public RenderDebugStageType Type { get; }
        public int Order { get; }
        public IReadOnlyList<RenderDebugChannelInfo> Channels => _channels ?? NoChannels;

        public bool Equals(RenderDebugStage other)
        {
            if (!string.Equals(Id, other.Id, StringComparison.Ordinal) ||
                !string.Equals(DisplayName, other.DisplayName, StringComparison.Ordinal) ||
                !string.Equals(Description, other.Description, StringComparison.Ordinal) ||
                !string.Equals(Group, other.Group, StringComparison.Ordinal) ||
                Type != other.Type ||
                Order != other.Order ||
                Channels.Count != other.Channels.Count)
            {
                return false;
            }

            for (int i = 0; i < Channels.Count; i++)
            {
                if (!Channels[i].Equals(other.Channels[i]))
                    return false;
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is RenderDebugStage other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Id != null ? StringComparer.Ordinal.GetHashCode(Id) : 0;
                hash = (hash * 397) ^ Order;
                hash = (hash * 397) ^ (int)Type;
                return hash;
            }
        }
    }
}