// -----------------------------------------------------------------------
// <copyright file="CallbackLock.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.Kinect.Toolkit
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading;

    public delegate void LockExitEventHandler();

    /// <summary>
    /// Utility class that encapsulates critical section-like locking
    /// and an event that gets fired after we exit the lock.  Its
    /// purpose in life is to delay calling event handlers until after
    /// we exit the lock.  If you call event handlers while you hold a
    /// lock it's easy to deadlock.  Those event handlers could
    /// choose to block on something on a different thread that's
    /// waiting on our lock.
    /// </summary>
    public sealed class CallbackLock : IDisposable
    {
        private readonly object lockObject;

        public CallbackLock(object lockTarget)
        {
            this.lockObject = lockTarget;
            Monitor.Enter(lockTarget);
        }

        [SuppressMessage("Microsoft.Design", "CA1009:DeclareEventHandlersCorrectly", Justification = "Helper event to defer actions until after lock exit doesn't need arguments")]
        public event LockExitEventHandler LockExit;

        public void Dispose()
        {
            Monitor.Exit(lockObject);
            if (LockExit != null)
            {
                LockExit();
            }
        }
    }
}
