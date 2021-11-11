// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using Microsoft.Coyote.Runtime;
using SystemThreading = System.Threading;

#pragma warning disable SA1005
#pragma warning disable SA1513

namespace Microsoft.Coyote.Interception
{
    /// <summary>
    /// Provides methods for monitors that can be controlled during testing.
    /// </summary>
    /// <remarks>This type is intended for compiler use rather than use directly in code.</remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static class ControlledMonitor
    {
        /// <summary>
        /// Determines whether the current thread holds the lock on the specified object.
        /// </summary>
        public static bool IsEntered(object obj)
        {
            Console.WriteLine("Checking monitor lock on thread {0}", Thread.CurrentThread.ManagedThreadId);
            // if (CoyoteRuntime.IsExecutionControlled)
            // {
            //     var mock = SynchronizedBlock.Mock.Find(obj);
            //     if (mock is null)
            //     {
            //         throw new SynchronizationLockException();
            //     }

            //     return mock.IsEntered();
            // }

            return Monitor.IsEntered(obj);
        }

        /// <summary>
        /// Acquires an exclusive lock on the specified object.
        /// </summary>
        public static void Enter(object obj)
        {
            Console.WriteLine("Entering monitor lock on thread {0}", Thread.CurrentThread.ManagedThreadId);
            var runtime = CoyoteRuntime.Current;
            if (runtime.SchedulingPolicy is SchedulingPolicy.Systematic)
            {
                var op = runtime.GetExecutingOperation<AsyncOperation>();
                op.Scope = AsyncOperationScope.Synchronized;
                Monitor.Enter(obj);
                if (op.Status is AsyncOperationStatus.BlockedOnResource)
                {
                    op.Status = AsyncOperationStatus.Enabled;
                    runtime.SyncPauseOperation(op);
                }
                else
                {
                    op.Scope = AsyncOperationScope.Default;
                }

                return;
            }
            // var runtime = CoyoteRuntime.Current;
            // if (runtime.SchedulingPolicy is SchedulingPolicy.Systematic)
            // {
            //     var op = runtime.GetExecutingOperation<AsyncOperation>();
            //     op.Scope = AsyncOperationScope.Synchronized;
            // }

            Monitor.Enter(obj);
            // op.Scope = AsyncOperationScope.Default;
            // Console.WriteLine("Entering monitor lock on thread {0}", Thread.CurrentThread.ManagedThreadId);
            // // if (CoyoteRuntime.IsExecutionControlled)
            // // {
            // //     SynchronizedBlock.Mock.Create(obj).Lock();
            // // }
            // // else
            // {
            //     Monitor.Enter(obj);
            // }
        }

        /// <summary>
        /// Acquires an exclusive lock on the specified object.
        /// </summary>
        public static void Enter(object obj, ref bool lockTaken)
        {
            Console.WriteLine("Entering monitor lock on thread {0}", Thread.CurrentThread.ManagedThreadId);
            var runtime = CoyoteRuntime.Current;
            if (runtime.SchedulingPolicy is SchedulingPolicy.Systematic)
            {
                var op = runtime.GetExecutingOperation<AsyncOperation>();
                op.Scope = AsyncOperationScope.Synchronized;
                Monitor.Enter(obj, ref lockTaken);
                if (op.Status is AsyncOperationStatus.BlockedOnResource)
                {
                    op.Status = AsyncOperationStatus.Enabled;
                    runtime.SyncPauseOperation(op);
                }
                else
                {
                    op.Scope = AsyncOperationScope.Default;
                }

                return;
            }

            Monitor.Enter(obj, ref lockTaken);
            // Console.WriteLine("Entering monitor lock on thread {0}", Thread.CurrentThread.ManagedThreadId);
            // // if (CoyoteRuntime.IsExecutionControlled)
            // // {
            // //     var block = SynchronizedBlock.Mock.Create(obj);
            // //     block.Lock();
            // //     lockTaken = block.IsLockTaken;
            // // }
            // // else
            // {
            //     Monitor.Enter(obj, ref lockTaken);
            // }
        }

        /// <summary>
        /// Attempts to acquire an exclusive lock on the specified object.
        /// </summary>
        public static bool TryEnter(object obj)
        {
            Console.WriteLine("Trying monitor lock on thread {0}", Thread.CurrentThread.ManagedThreadId);
            // var runtime = CoyoteRuntime.Current;
            // if (runtime.SchedulingPolicy is SchedulingPolicy.Systematic)
            // {
            //     var op = runtime.GetExecutingOperation<AsyncOperation>();
            //     op.Scope = AsyncOperationScope.Synchronized;
            // }

            var result = Monitor.TryEnter(obj);
            // op.Scope = AsyncOperationScope.Default;
            return result;
            // Console.WriteLine("Trying monitor lock on thread {0}", Thread.CurrentThread.ManagedThreadId);
            // // if (CoyoteRuntime.IsExecutionControlled)
            // // {
            // //     var block = SynchronizedBlock.Mock.Create(obj);
            // //     block.Lock();
            // //     return block.IsLockTaken;
            // // }

            // return Monitor.TryEnter(obj);
        }

        /// <summary>
        /// Attempts, for the specified number of milliseconds, to acquire an exclusive lock
        /// on the specified object.
        /// </summary>
        public static bool TryEnter(object obj, int millisecondsTimeout)
        {
            Console.WriteLine("Trying monitor lock on thread {0}", Thread.CurrentThread.ManagedThreadId);
            return Monitor.TryEnter(obj, millisecondsTimeout);
            // Console.WriteLine("Trying monitor lock on thread {0}", Thread.CurrentThread.ManagedThreadId);
            // // if (CoyoteRuntime.IsExecutionControlled)
            // // {
            // //     // TODO: how to implement this timeout?
            // //     var block = SynchronizedBlock.Mock.Create(obj);
            // //     block.Lock();
            // //     return block.IsLockTaken;
            // // }

            // return Monitor.TryEnter(obj, millisecondsTimeout);
        }

        /// <summary>
        /// Attempts, for the specified amount of time, to acquire an exclusive lock on the specified object,
        /// and atomically sets a value that indicates whether the lock was taken.
        /// </summary>
        public static bool TryEnter(object obj, TimeSpan timeout)
        {
            Console.WriteLine("Trying monitor lock on thread {0}", Thread.CurrentThread.ManagedThreadId);
            return Monitor.TryEnter(obj, timeout);
            // Console.WriteLine("Trying monitor lock on thread {0}", Thread.CurrentThread.ManagedThreadId);
            // // if (CoyoteRuntime.IsExecutionControlled)
            // // {
            // //     // TODO: how to implement this timeout?
            // //     var block = SynchronizedBlock.Mock.Create(obj);
            // //     block.Lock();
            // //     return block.IsLockTaken;
            // // }

            // return Monitor.TryEnter(obj, timeout);
        }

        /// <summary>
        /// Attempts to acquire an exclusive lock on the specified object, and atomically
        /// sets a value that indicates whether the lock was taken.
        /// </summary>
        public static void TryEnter(object obj, ref bool lockTaken)
        {
            Console.WriteLine("Trying monitor lock on thread {0}", Thread.CurrentThread.ManagedThreadId);
            Monitor.TryEnter(obj, ref lockTaken);
            // Console.WriteLine("Trying monitor lock on thread {0}", Thread.CurrentThread.ManagedThreadId);
            // // if (CoyoteRuntime.IsExecutionControlled)
            // // {
            // //     // TODO: how to implement this timeout?
            // //     var block = SynchronizedBlock.Mock.Create(obj);
            // //     block.Lock();
            // //     lockTaken = block.IsLockTaken;
            // // }
            // // else
            // {
            //     Monitor.TryEnter(obj, ref lockTaken);
            // }
        }

        /// <summary>
        /// Attempts, for the specified number of milliseconds, to acquire an exclusive lock on the specified object,
        /// and atomically sets a value that indicates whether the lock was taken.
        /// </summary>
        public static void TryEnter(object obj, int millisecondsTimeout, ref bool lockTaken)
        {
            Console.WriteLine("Trying monitor lock on thread {0}", Thread.CurrentThread.ManagedThreadId);
            Monitor.TryEnter(obj, millisecondsTimeout, ref lockTaken);
            // Console.WriteLine("Trying monitor lock on thread {0}", Thread.CurrentThread.ManagedThreadId);
            // // if (CoyoteRuntime.IsExecutionControlled)
            // // {
            // //     // TODO: how to implement this timeout?
            // //     var block = SynchronizedBlock.Mock.Create(obj);
            // //     block.Lock();
            // //     lockTaken = block.IsLockTaken;
            // // }
            // // else
            // {
            //     Monitor.TryEnter(obj, millisecondsTimeout, ref lockTaken);
            // }
        }

        /// <summary>
        /// Attempts, for the specified amount of time, to acquire an exclusive lock on the specified object,
        /// and atomically sets a value that indicates whether the lock was taken.
        /// </summary>
        public static void TryEnter(object obj, TimeSpan timeout, ref bool lockTaken)
        {
            Console.WriteLine("Trying monitor lock on thread {0}", Thread.CurrentThread.ManagedThreadId);
            Monitor.TryEnter(obj, timeout, ref lockTaken);
            // Console.WriteLine("Trying monitor lock on thread {0}", Thread.CurrentThread.ManagedThreadId);
            // // if (CoyoteRuntime.IsExecutionControlled)
            // // {
            // //     // TODO: how to implement this timeout?
            // //     var block = SynchronizedBlock.Mock.Create(obj);
            // //     block.Lock();
            // //     lockTaken = block.IsLockTaken;
            // // }
            // // else
            // {
            //     Monitor.TryEnter(obj, timeout, ref lockTaken);
            // }
        }

        /// <summary>
        /// Releases the lock on an object and blocks the current thread until it reacquires the lock.
        /// </summary>
        public static bool Wait(object obj)
        {
            Console.WriteLine("Waiting on monitor lock on thread {0}", Thread.CurrentThread.ManagedThreadId);
            var runtime = CoyoteRuntime.Current;
            if (runtime.SchedulingPolicy is SchedulingPolicy.Systematic)
            {
                var op = runtime.GetExecutingOperation<AsyncOperation>();
                op.Scope = AsyncOperationScope.Synchronized;
                var result = Monitor.Wait(obj);
                if (op.Status is AsyncOperationStatus.BlockedOnResource)
                {
                    op.Status = AsyncOperationStatus.Enabled;
                    runtime.SyncPauseOperation(op);
                }
                else
                {
                    op.Scope = AsyncOperationScope.Default;
                }

                return result;
            }

            return Monitor.Wait(obj);
            // Console.WriteLine("Waiting on monitor lock on thread {0}", Thread.CurrentThread.ManagedThreadId);
            // // var runtime = CoyoteRuntime.Current;
            // // if (runtime.SchedulingPolicy is SchedulingPolicy.Systematic)
            // // {
            // //     var op = runtime.GetExecutingOperation<AsyncOperation>();
            // //     op.Status = AsyncOperationStatus.BlockedOnResource;
            // //     // this.AwaitingOperations.Add(op);
            // //     runtime.ScheduleNextOperation(AsyncOperationType.Join, false, true);
            // // }

            // // if (CoyoteRuntime.IsExecutionControlled)
            // // {
            // //     var mock = SynchronizedBlock.Mock.Find(obj);
            // //     if (mock is null)
            // //     {
            // //         throw new SynchronizationLockException();
            // //     }

            // //     return mock.Wait();
            // // }

            // return Monitor.Wait(obj);
        }

        /// <summary>
        /// Releases the lock on an object and blocks the current thread until it reacquires the lock.
        /// If the specified time-out interval elapses, the thread enters the ready queue.
        /// </summary>
        public static bool Wait(object obj, int millisecondsTimeout)
        {
            Console.WriteLine("Waiting on monitor lock on thread {0}", Thread.CurrentThread.ManagedThreadId);
            return Monitor.Wait(obj, millisecondsTimeout);
            // Console.WriteLine("Waiting on monitor lock on thread {0}", Thread.CurrentThread.ManagedThreadId);
            // // if (CoyoteRuntime.IsExecutionControlled)
            // // {
            // //     var mock = SynchronizedBlock.Mock.Find(obj);
            // //     if (mock is null)
            // //     {
            // //         throw new SynchronizationLockException();
            // //     }

            // //     return mock.Wait(millisecondsTimeout);
            // // }

            // return Monitor.Wait(obj, millisecondsTimeout);
        }

        /// <summary>
        /// Releases the lock on an object and blocks the current thread until it reacquires the lock.
        /// If the specified time-out interval elapses, the thread enters the ready queue.
        /// </summary>
        public static bool Wait(object obj, TimeSpan timeout)
        {
            Console.WriteLine("Waiting on monitor lock on thread {0}", Thread.CurrentThread.ManagedThreadId);
            return Monitor.Wait(obj, timeout);
            // Console.WriteLine("Waiting on monitor lock on thread {0}", Thread.CurrentThread.ManagedThreadId);
            // // if (CoyoteRuntime.IsExecutionControlled)
            // // {
            // //     var mock = SynchronizedBlock.Mock.Find(obj);
            // //     if (mock is null)
            // //     {
            // //         throw new SynchronizationLockException();
            // //     }

            // //     return mock.Wait(timeout);
            // // }

            // return Monitor.Wait(obj, timeout);
        }

        /// <summary>
        /// Releases the lock on an object and blocks the current thread until it reacquires the lock. If the
        /// specified time-out interval elapses, the thread enters the ready queue. This method also specifies
        /// whether the synchronization domain for the context (if in a synchronized context) is exited before
        /// the wait and reacquired afterward.
        /// </summary>
        public static bool Wait(object obj, int millisecondsTimeout, bool exitContext)
        {
            Console.WriteLine("Waiting on monitor lock on thread {0}", Thread.CurrentThread.ManagedThreadId);
            return Monitor.Wait(obj, millisecondsTimeout, exitContext);
            // Console.WriteLine("Waiting on monitor lock on thread {0}", Thread.CurrentThread.ManagedThreadId);
            // // if (CoyoteRuntime.IsExecutionControlled)
            // // {
            // //     var mock = SynchronizedBlock.Mock.Find(obj);
            // //     if (mock is null)
            // //     {
            // //         throw new SynchronizationLockException();
            // //     }

            // //     // TODO: implement exitContext.
            // //     return mock.Wait(millisecondsTimeout);
            // // }

            // return Monitor.Wait(obj, millisecondsTimeout, exitContext);
        }

        /// <summary>
        /// Releases the lock on an object and blocks the current thread until it reacquires the lock.
        /// If the specified time-out interval elapses, the thread enters the ready queue. Optionally
        /// exits the synchronization domain for the synchronized context before the wait and reacquires
        /// the domain afterward.
        /// </summary>
        public static bool Wait(object obj, TimeSpan timeout, bool exitContext)
        {
            Console.WriteLine("Waiting on monitor lock on thread {0}", Thread.CurrentThread.ManagedThreadId);
            return Monitor.Wait(obj, timeout, exitContext);
            // Console.WriteLine("Waiting on monitor lock on thread {0}", Thread.CurrentThread.ManagedThreadId);
            // // if (CoyoteRuntime.IsExecutionControlled)
            // // {
            // //     var mock = SynchronizedBlock.Mock.Find(obj);
            // //     if (mock is null)
            // //     {
            // //         throw new SynchronizationLockException();
            // //     }

            // //     // TODO: implement exitContext.
            // //     return mock.Wait(timeout);
            // // }

            // return Monitor.Wait(obj, timeout, exitContext);
        }

        /// <summary>
        /// Releases an exclusive lock on the specified object.
        /// </summary>
        public static void Exit(object obj)
        {
            Console.WriteLine("Exiting monitor lock on thread {0}", Thread.CurrentThread.ManagedThreadId);
            // var runtime = CoyoteRuntime.Current;
            // if (runtime.SchedulingPolicy is SchedulingPolicy.Systematic)
            // {
            //     var op = runtime.GetExecutingOperation<AsyncOperation>();
            //     op.Scope = AsyncOperationScope.Synchronized;
            // }

            Monitor.Exit(obj);
            // op.Scope = AsyncOperationScope.Default;
            // Console.WriteLine("Exiting monitor lock on thread {0}", Thread.CurrentThread.ManagedThreadId);
            // // if (CoyoteRuntime.IsExecutionControlled)
            // // {
            // //     var mock = SynchronizedBlock.Mock.Find(obj);
            // //     if (mock is null)
            // //     {
            // //         throw new SynchronizationLockException();
            // //     }

            // //     mock.Exit();
            // // }
            // // else
            // {
            //     Monitor.Exit(obj);
            // }
        }

        /// <summary>
        /// Notifies a thread in the waiting queue of a change in the locked object's state.
        /// </summary>
        public static void Pulse(object obj)
        {
            Console.WriteLine("==== Pulsing monitor lock on thread {0}", Thread.CurrentThread.ManagedThreadId);
            // var runtime = CoyoteRuntime.Current;
            // if (runtime.SchedulingPolicy is SchedulingPolicy.Systematic)
            // {
            //     var op = runtime.GetExecutingOperation<AsyncOperation>();
            //     op.Scope = AsyncOperationScope.Synchronized;
            // }

            Monitor.Pulse(obj);
            // op.Scope = AsyncOperationScope.Default;
            // Console.WriteLine("Pulsing monitor lock on thread {0}", Thread.CurrentThread.ManagedThreadId);
            // // if (CoyoteRuntime.IsExecutionControlled)
            // // {
            // //     var mock = SynchronizedBlock.Mock.Find(obj);
            // //     if (mock is null)
            // //     {
            // //         throw new SynchronizationLockException();
            // //     }

            // //     mock.Pulse();
            // // }
            // // else
            // {
            //     Monitor.Pulse(obj);
            // }
        }

        /// <summary>
        /// Notifies all waiting threads of a change in the object's state.
        /// </summary>
        public static void PulseAll(object obj)
        {
            Console.WriteLine("==== Pulsing all monitor lock on thread {0}", Thread.CurrentThread.ManagedThreadId);
            // var runtime = CoyoteRuntime.Current;
            // if (runtime.SchedulingPolicy is SchedulingPolicy.Systematic)
            // {
            //     var op = runtime.GetExecutingOperation<AsyncOperation>();
            //     op.Scope = AsyncOperationScope.Synchronized;
            // }

            Monitor.PulseAll(obj);
            // op.Scope = AsyncOperationScope.Default;
            // Console.WriteLine("Pulsing all monitor lock on thread {0}", Thread.CurrentThread.ManagedThreadId);
            // // if (CoyoteRuntime.IsExecutionControlled)
            // // {
            // //     var mock = SynchronizedBlock.Mock.Find(obj);
            // //     if (mock is null)
            // //     {
            // //         throw new SynchronizationLockException();
            // //     }

            // //     mock.PulseAll();
            // // }
            // // else
            // {
            //     Monitor.PulseAll(obj);
            // }
        }
    }
}
