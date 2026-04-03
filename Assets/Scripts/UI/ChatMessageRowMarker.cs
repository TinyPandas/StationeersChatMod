using UnityEngine;

namespace ChatMod.UI
{
    /// <summary>Tracks which <see cref="ChatEntry.Sequence"/> a spawned message row represents (incremental list sync).</summary>
    public sealed class ChatMessageRowMarker : MonoBehaviour
    {
        public long Sequence { get; set; }
    }
}
