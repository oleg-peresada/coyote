// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;

namespace Microsoft.Coyote.Runtime
{
    /// <summary>
    /// The synchronization context where controlled operations are executed.
    /// </summary>
    internal sealed class ControlledSynchronizationContext : SynchronizationContext, IDisposable
    {
        /// <summary>
        /// Responsible for controlling the execution of operations during systematic testing.
        /// </summary>
        internal CoyoteRuntime Runtime { get; private set; }

        /// <summary>
        /// The original synchronization context.
        /// </summary>
        internal readonly SynchronizationContext Original;

        /// <summary>
        /// Initializes a new instance of the <see cref="ControlledSynchronizationContext"/> class.
        /// </summary>
        internal ControlledSynchronizationContext(CoyoteRuntime runtime)
        {
            this.Runtime = runtime;
            this.Original = Current;
            if (this.Runtime.SchedulingPolicy is SchedulingPolicy.Systematic)
            {
                this.SetWaitNotificationRequired();
            }
        }

        /// <inheritdoc/>
        public override void Post(SendOrPostCallback d, object state)
        {
            try
            {
                IO.Debug.WriteLine("<ScheduleDebug> Posting callback from thread '{0}'.",
                    Thread.CurrentThread.ManagedThreadId);
                this.Runtime?.Schedule(() => d(state));
            }
            catch (ThreadInterruptedException)
            {
                // Ignore the thread interruption.
            }
        }

        /// <inheritdoc/>
        public override int Wait(IntPtr[] waitHandles, bool waitAll, int millisecondsTimeout)
        {
            if (this.Runtime.SchedulingPolicy is SchedulingPolicy.Systematic)
            {
                try
                {
                    SetSynchronizationContext(this.Original);
                    IO.Debug.WriteLine("<ScheduleDebug> Waiting in thread '{0}':\n{1}",
                        Thread.CurrentThread.ManagedThreadId, new System.Diagnostics.StackTrace().ToString());
                    if (this.Runtime.TryGetExecutingOperationIfScheduled(out AsyncOperation op) &&
                        op.Scope is AsyncOperationScope.Synchronized)
                    {
                        op.Scope = AsyncOperationScope.Default;
                        op.Status = AsyncOperationStatus.BlockedOnResource;
                        IO.Debug.WriteLine(">>>>>>>>>>>>>>>>> Operation '{0}' is waiting in thread '{1}'.",
                            op.Id, Thread.CurrentThread.ManagedThreadId);
                        // Environment.Exit(1);
                        this.Runtime.ScheduleNextOperation(AsyncOperationType.Join, isPausing: false);
                    }
                }
                finally
                {
                    SetSynchronizationContext(this);
                }
            }

            return base.Wait(waitHandles, waitAll, millisecondsTimeout);
        }

        /// <inheritdoc/>
        public override SynchronizationContext CreateCopy() => this;

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Runtime = null;
        }
    }
}
