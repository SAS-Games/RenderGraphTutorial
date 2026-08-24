using System;

namespace SAS.RenderDebugging
{
    /// <summary>Describes the effect-defined meaning of one packed texture channel.</summary>
    public readonly struct RenderDebugChannelInfo : IEquatable<RenderDebugChannelInfo>
    {
        public RenderDebugChannelInfo(string channel, string meaning)
        {
            Channel = channel ?? string.Empty;
            Meaning = meaning ?? string.Empty;
        }

        /// <summary>Gets the channel label, normally R, G, B, or A.</summary>
        public string Channel { get; }

        /// <summary>Gets the semantic meaning supplied by the effect.</summary>
        public string Meaning { get; }

        public bool Equals(RenderDebugChannelInfo other)
        {
            return string.Equals(Channel, other.Channel, StringComparison.Ordinal) &&
                   string.Equals(Meaning, other.Meaning, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is RenderDebugChannelInfo other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Channel != null ? StringComparer.Ordinal.GetHashCode(Channel) : 0) * 397) ^
                       (Meaning != null ? StringComparer.Ordinal.GetHashCode(Meaning) : 0);
            }
        }
    }
}
