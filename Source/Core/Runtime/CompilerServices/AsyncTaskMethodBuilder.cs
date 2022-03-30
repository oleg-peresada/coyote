// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using SystemCompiler = System.Runtime.CompilerServices;

namespace Microsoft.Coyote.Runtime.CompilerServices
{
    /// <summary>
    /// Represents a builder for asynchronous methods that return a controlled task.
    /// This type is intended for compiler use only.
    /// </summary>
    /// <remarks>This type is intended for compiler use rather than use directly in code.</remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [StructLayout(LayoutKind.Auto)]
    public struct AsyncTaskMethodBuilder
    {
        /// <summary>
        /// Responsible for controlling the execution of tasks during systematic testing.
        /// </summary>
        private readonly CoyoteRuntime Runtime;

        /// <summary>
        /// The task builder to which most operations are delegated.
        /// </summary>
#pragma warning disable IDE0044 // Add readonly modifier
        private SystemCompiler.AsyncTaskMethodBuilder MethodBuilder;
#pragma warning restore IDE0044 // Add readonly modifier

        private AsyncOperation ParentOperation;

        /// <summary>
        /// Gets the task for this builder.
        /// </summary>
        public Task Task
        {
            [DebuggerHidden]
            get
            {
                IO.Debug.WriteLine("<AsyncBuilder> Creating builder task '{0}' from thread '{1}' (isCompleted {2}).",
                    this.MethodBuilder.Task.Id, Thread.CurrentThread.ManagedThreadId, this.MethodBuilder.Task.IsCompleted);
                this.Runtime?.OnTaskCompletionSourceGetTask(this.MethodBuilder.Task);
                return this.MethodBuilder.Task;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncTaskMethodBuilder"/> struct.
        /// </summary>
        private AsyncTaskMethodBuilder(CoyoteRuntime runtime)
        {
            this.Runtime = runtime;
            this.MethodBuilder = default;
            this.ParentOperation = default;
        }

        /// <summary>
        /// Creates an instance of the <see cref="AsyncTaskMethodBuilder"/> struct.
        /// </summary>
        public static AsyncTaskMethodBuilder Create()
        {
            CoyoteRuntime runtime = null;
            if (SynchronizationContext.Current is ControlledSynchronizationContext controlledContext &&
                controlledContext.Runtime.SchedulingPolicy != SchedulingPolicy.None)
            {
                runtime = controlledContext.Runtime;
            }

            return new AsyncTaskMethodBuilder(runtime);
        }

        /// <summary>
        /// Begins running the builder with the associated state machine.
        /// </summary>
        [DebuggerStepThrough]
        public void Start<TStateMachine>(ref TStateMachine stateMachine)
            where TStateMachine : IAsyncStateMachine
        {
            this.ParentOperation = CoyoteRuntime.ThreadLocalParentAsyncOperation.Value;
            if (this.ParentOperation == null)
            {
                this.Runtime?.OnAsyncStateMachineStart(true);
            }
            else
            {
                this.Runtime?.OnAsyncStateMachineStart(false);
            }

            IO.Debug.WriteLine("<AsyncBuilder> Start state machine from thread '{0}' with context '{1}' and runtime '{2}'.",
                Thread.CurrentThread.ManagedThreadId, SynchronizationContext.Current, this.Runtime?.Id);
            IO.Debug.WriteLine($"===========<F_AsyncBuilder> [Start] thread: {Thread.CurrentThread.ManagedThreadId}, Task: {Task.CurrentId}, tlid: {CoyoteRuntime.ThreadLocalParentAsyncOperation?.Value}");
            this.MethodBuilder.Start(ref stateMachine);
        }

        /// <summary>
        /// Associates the builder with the specified state machine.
        /// </summary>
        public void SetStateMachine(IAsyncStateMachine stateMachine) =>
            this.MethodBuilder.SetStateMachine(stateMachine);

        /// <summary>
        /// Marks the task as successfully completed.
        /// </summary>
        public void SetResult()
        {
            IO.Debug.WriteLine("<AsyncBuilder> Set result of task '{0}' from thread '{1}'.",
                this.MethodBuilder.Task.Id, Thread.CurrentThread.ManagedThreadId);
            this.MethodBuilder.SetResult();
        }

        /// <summary>
        /// Callback to AsyncTaskMethodBuilder before MoveNext method at IL level.
        /// </summary>
        // [DebuggerHidden]
        public void OnMoveNext()
        {
            IO.Debug.WriteLine($"===========<F_AsyncBuilder> [onMoveNext] ParentOperation: {this.ParentOperation}, thread: {Thread.CurrentThread.ManagedThreadId}, Task: {Task.CurrentId}, tlid: {CoyoteRuntime.ThreadLocalParentAsyncOperation?.Value}");
            this.Runtime?.SetParentOnMoveNext(this.ParentOperation);
        }

        /// <summary>
        /// Marks the task as failed and binds the specified exception to the task.
        /// </summary>
        public void SetException(Exception exception) => this.MethodBuilder.SetException(exception);

        /// <summary>
        /// Schedules the state machine to proceed to the next action when the specified awaiter completes.
        /// </summary>
        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine =>
            this.MethodBuilder.AwaitOnCompleted(ref awaiter, ref stateMachine);

        /// <summary>
        /// Schedules the state machine to proceed to the next action when the specified awaiter completes.
        /// </summary>
        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
            {
                IO.Debug.WriteLine($"===========<F_AsyncBuilder> [AwaitUnsafeOnCompleted] thread: {Thread.CurrentThread.ManagedThreadId}, Task: {Task.CurrentId}, tlid: {CoyoteRuntime.ThreadLocalParentAsyncOperation?.Value}");

                // this.Runtime?.OnAwaitUnsafeOnCompleted(this.ParentOperation);
                // if (this.Runtime != null)
                // {
                //     this.Runtime.YieldCaseFlag = true;
                // }

                this.MethodBuilder.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);
            }
    }

    /// <summary>
    /// Represents a builder for asynchronous methods that return a <see cref="Task{TResult}"/>.
    /// This type is intended for compiler use only.
    /// </summary>
    /// <remarks>This type is intended for compiler use rather than use directly in code.</remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [StructLayout(LayoutKind.Auto)]
    public struct AsyncTaskMethodBuilder<TResult>
    {
        /// <summary>
        /// Responsible for controlling the execution of tasks during systematic testing.
        /// </summary>
        private readonly CoyoteRuntime Runtime;

        /// <summary>
        /// The task builder to which most operations are delegated.
        /// </summary>
#pragma warning disable IDE0044 // Add readonly modifier
        private SystemCompiler.AsyncTaskMethodBuilder<TResult> MethodBuilder;
#pragma warning restore IDE0044 // Add readonly modifier

        private AsyncOperation ParentOperation;

        /// <summary>
        /// Gets the task for this builder.
        /// </summary>
        public Task<TResult> Task
        {
            [DebuggerHidden]
            get
            {
                IO.Debug.WriteLine("<AsyncBuilder> Creating builder task '{0}' from thread '{1}' (isCompleted {2}).",
                    this.MethodBuilder.Task.Id, Thread.CurrentThread.ManagedThreadId, this.MethodBuilder.Task.IsCompleted);
                this.Runtime?.OnTaskCompletionSourceGetTask(this.MethodBuilder.Task);
                return this.MethodBuilder.Task;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncTaskMethodBuilder{TResult}"/> struct.
        /// </summary>
        private AsyncTaskMethodBuilder(CoyoteRuntime runtime)
        {
            this.Runtime = runtime;
            this.MethodBuilder = default;
            this.ParentOperation = default;
        }

        /// <summary>
        /// Creates an instance of the <see cref="AsyncTaskMethodBuilder{TResult}"/> struct.
        /// </summary>
#pragma warning disable CA1000 // Do not declare static members on generic types
        public static AsyncTaskMethodBuilder<TResult> Create()
        {
            CoyoteRuntime runtime = null;
            if (SynchronizationContext.Current is ControlledSynchronizationContext controlledContext &&
                controlledContext.Runtime.SchedulingPolicy != SchedulingPolicy.None)
            {
                runtime = controlledContext.Runtime;
            }

            return new AsyncTaskMethodBuilder<TResult>(runtime);
        }
#pragma warning restore CA1000 // Do not declare static members on generic types

        /// <summary>
        /// Begins running the builder with the associated state machine.
        /// </summary>
        [DebuggerStepThrough]
        public void Start<TStateMachine>(ref TStateMachine stateMachine)
            where TStateMachine : IAsyncStateMachine
        {
            this.ParentOperation = CoyoteRuntime.ThreadLocalParentAsyncOperation.Value;
            if (this.ParentOperation == null)
            {
                this.Runtime?.OnAsyncStateMachineStart(true);
            }
            else
            {
                this.Runtime?.OnAsyncStateMachineStart(false);
            }

            IO.Debug.WriteLine("<AsyncBuilder> Start state machine from thread '{0}' with context '{1}' and runtime '{2}'.",
                Thread.CurrentThread.ManagedThreadId, SynchronizationContext.Current, this.Runtime?.Id);
            IO.Debug.WriteLine($"===========<F_AsyncBuilder> [Start] thread {Thread.CurrentThread.ManagedThreadId}, Task: ?, tlid: {CoyoteRuntime.ThreadLocalParentAsyncOperation?.Value}");
            this.MethodBuilder.Start(ref stateMachine);
        }

        /// <summary>
        /// Associates the builder with the specified state machine.
        /// </summary>
        public void SetStateMachine(IAsyncStateMachine stateMachine) =>
            this.MethodBuilder.SetStateMachine(stateMachine);

        /// <summary>
        /// Marks the task as successfully completed.
        /// </summary>
        /// <param name="result">The result to use to complete the task.</param>
        public void SetResult(TResult result)
        {
            IO.Debug.WriteLine("<AsyncBuilder> Set result of task '{0}' from thread '{1}'.",
                this.MethodBuilder.Task.Id, Thread.CurrentThread.ManagedThreadId);
            this.MethodBuilder.SetResult(result);
        }

        /// <summary>
        /// Callback to AsyncTaskMethodBuilder before MoveNext method at IL level.
        /// </summary>
        // [DebuggerHidden]
        public void OnMoveNext()
        {
            IO.Debug.WriteLine($"===========<F_AsyncBuilder> [onMoveNext] ParentOperation: {this.ParentOperation}, thread: {Thread.CurrentThread.ManagedThreadId}, Task: ?, tlid: {CoyoteRuntime.ThreadLocalParentAsyncOperation?.Value}");
            this.Runtime?.SetParentOnMoveNext(this.ParentOperation);
        }

        /// <summary>
        /// Marks the task as failed and binds the specified exception to the task.
        /// </summary>
        public void SetException(Exception exception) => this.MethodBuilder.SetException(exception);

        /// <summary>
        /// Schedules the state machine to proceed to the next action when the specified awaiter completes.
        /// </summary>
        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
                where TAwaiter : INotifyCompletion
                where TStateMachine : IAsyncStateMachine =>
            this.MethodBuilder.AwaitOnCompleted(ref awaiter, ref stateMachine);

        /// <summary>
        /// Schedules the state machine to proceed to the next action when the specified awaiter completes.
        /// </summary>
        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
            {
                IO.Debug.WriteLine($"===========<F_AsyncBuilder> [AwaitUnsafeOnCompleted] thread {Thread.CurrentThread.ManagedThreadId}, Task: ?, tlid: {CoyoteRuntime.ThreadLocalParentAsyncOperation?.Value}");

                // this.Runtime?.OnAwaitUnsafeOnCompleted(this.ParentOperation);
                // if (this.Runtime != null)
                // {
                //     this.Runtime.YieldCaseFlag = true;
                // }

                this.MethodBuilder.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);
            }
    }
}
