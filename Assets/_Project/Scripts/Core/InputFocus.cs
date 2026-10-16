using System;
using UnityEngine;

namespace Cinderbird.Core
{
    /// <summary>
    /// Who owns the player's action buttons right now. Normally nobody, and the tools use them (scoop, shake, pour,
    /// use, place). While something takes the focus (e.g. choosing where to send a helper), the tools leave the
    /// buttons alone, so one click never does two things.
    /// </summary>
    public sealed class InputFocus : MonoBehaviour
    {
        private object _owner;
        private int _takenFrame = -1;
        private int _releasedFrame = -1;

        public event Action Changed;

        /// <summary>
        /// True while something owns the buttons, and also on the frame it took or gave them back: the click that ended
        /// a choice must not also scoop.
        /// </summary>
        public bool IsTaken => _owner != null || Time.frameCount == _takenFrame || Time.frameCount == _releasedFrame;

        public void Take(object owner)
        {
            if (owner == null || ReferenceEquals(owner, _owner))
            {
                return;
            }

            _owner = owner;
            _takenFrame = Time.frameCount;
            Changed?.Invoke();
        }

        public void Release(object owner)
        {
            if (!ReferenceEquals(owner, _owner))
            {
                return;
            }

            _owner = null;
            _releasedFrame = Time.frameCount;
            Changed?.Invoke();
        }
    }
}
