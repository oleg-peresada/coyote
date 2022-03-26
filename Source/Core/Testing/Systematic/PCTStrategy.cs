// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Coyote.IO;
using Microsoft.Coyote.Runtime;
using Microsoft.Coyote.Specifications;

namespace Microsoft.Coyote.Testing.Systematic
{
    /// <summary>
    /// A probabilistic priority-based scheduling strategy.
    /// </summary>
    /// <remarks>
    /// This strategy is described in the following paper:
    /// https://www.microsoft.com/en-us/research/wp-content/uploads/2016/02/asplos277-pct.pdf.
    /// </remarks>
    internal sealed class PCTStrategy : SystematicStrategy
    {
        internal sealed class AsyncStateMachineTaskOperationsGroup
        {
            private readonly IRandomValueGenerator RandomValueGenerator;

            private readonly int TaskGroupID;

            private readonly AsyncOperation OwnerOperation;

            private readonly List<AsyncOperation> OperationsChain;

            internal AsyncStateMachineTaskOperationsGroup(AsyncOperation parentOperation, IRandomValueGenerator generator)
            {
                this.RandomValueGenerator = generator;
                this.TaskGroupID = parentOperation.TaskGroupID;
                this.OwnerOperation = parentOperation;
                this.OperationsChain = new List<AsyncOperation>();
                this.OperationsChain.Add(parentOperation);
            }

            internal AsyncOperation GetOwnerOperation()
            {
                return this.OwnerOperation;
            }

            internal void InsertOperation(AsyncOperation newOperation)
            {
                // FN_TODO: think, is this randomization required?
                int index = this.RandomValueGenerator.Next(this.OperationsChain.Count) + 1;
                this.OperationsChain.Insert(index, newOperation);
            }

            internal void RemoveOperation(AsyncOperation operationToRemove)
            {
                Specification.Assert(this.OperationsChain.Contains(operationToRemove), $"     ===========<IMP_AsyncStateMachineTaskOperationsGroup-ERROR> [RemoveOperation] removing non present opeation from chain of owner : {this.OwnerOperation}");
                this.OperationsChain.Remove(operationToRemove);
            }

            internal AsyncOperation GiveRandomEnabledOperation()
            {
                // FN_TODO: think of this below assertion.
                // Specification.Assert(enabledOperationsInThisChain.Count >= 0, $"     <TaskSummaryLog-ERROR> No enabled operationto return in GiveFirstEnabledOperation call.");
                var enabledOperationsInThisChain = this.OperationsChain.Where(op => op.Status is AsyncOperationStatus.Enabled).ToList();
                if (enabledOperationsInThisChain.Count == 0)
                {
                    return null;
                }
                else
                {
                    int index = this.RandomValueGenerator.Next(enabledOperationsInThisChain.Count); // FN_TODO: think is +1 required here?
                    return enabledOperationsInThisChain[index];
                }
            }

            // FN_TODO: test this method also by replacing GiveRandomEnabledOperation with it at every callsite.
            internal AsyncOperation GiveFirstEnabledOperation()
            {
                // FN_TODO: think of this below assertion.
                // Specification.Assert(enabledOperationsInThisChain.Count >= 0, $"     <TaskSummaryLog-ERROR> No enabled operationto return in GiveFirstEnabledOperation call.");
                var enabledOperationsInThisChain = this.OperationsChain.Where(op => op.Status is AsyncOperationStatus.Enabled).ToList();
                if (enabledOperationsInThisChain.Count == 0)
                {
                    return null;
                }
                else
                {
                    return enabledOperationsInThisChain[0];
                }
            }

            internal List<AsyncOperation> GetOperationsChain()
            {
                return this.OperationsChain;
            }
        }

        private int ContextSwitchNumber;

        // private readonly int EnabledSpawneesCountMax = 0;

        // private int ActualNumberOfPrioritySwitches = 0;

        private readonly Dictionary<AsyncOperation, AsyncStateMachineTaskOperationsGroup> AsyncOperationToOperationsGroupMap;

        private AsyncStateMachineTaskOperationsGroup NonAsyncStateMachineOperationGroup;

        /// <summary>
        /// List of prioritized operations.
        /// </summary>
        private readonly List<AsyncStateMachineTaskOperationsGroup> PrioritizedOperations;

        private readonly List<AsyncOperation> AllRegisteredOperations;

        private readonly HashSet<AsyncOperation> RegisteredOps;

        /// <summary>
        /// Random value generator.
        /// </summary>
        private readonly IRandomValueGenerator RandomValueGenerator;

        /// <summary>
        /// The maximum number of steps to explore.
        /// </summary>
        private readonly int MaxSteps;

        /// <summary>
        /// The number of exploration steps.
        /// </summary>
        private int StepCount;

        /// <summary>
        /// Max number of priority switch points.
        /// </summary>
        private readonly int MaxPrioritySwitchPoints;

        /// <summary>
        /// Approximate length of the schedule across all iterations.
        /// </summary>
        private int ScheduleLength;

        /// <summary>
        /// Scheduling points in the current execution where a priority change should occur.
        /// </summary>
        private readonly HashSet<int> PriorityChangePoints;

        /// <summary>
        /// Initializes a new instance of the <see cref="PCTStrategy"/> class.
        /// </summary>
        internal PCTStrategy(int maxSteps, int maxPrioritySwitchPoints, IRandomValueGenerator generator)
        {
            this.RandomValueGenerator = generator;
            this.MaxSteps = maxSteps;
            this.StepCount = 0;
            this.ScheduleLength = 0;
            this.MaxPrioritySwitchPoints = maxPrioritySwitchPoints;
            this.PriorityChangePoints = new HashSet<int>();
            this.ContextSwitchNumber = 0;
            // this.EnabledSpawneesCountMax = 0;
            // this.ActualNumberOfPrioritySwitches = 0;
            this.AsyncOperationToOperationsGroupMap = new Dictionary<AsyncOperation, AsyncStateMachineTaskOperationsGroup>();
            // this.NonAsyncStateMachineOperationGroup = new AsyncStateMachineTaskOperationsGroup();
            this.PrioritizedOperations = new List<AsyncStateMachineTaskOperationsGroup>();
            this.AllRegisteredOperations = new List<AsyncOperation>();
            this.RegisteredOps = new HashSet<AsyncOperation>();
        }

        /// <inheritdoc/>
        internal override bool InitializeNextIteration(uint iteration)
        {
            // The first iteration has no knowledge of the execution, so only initialize from the second
            // iteration and onwards. Note that although we could initialize the first length based on a
            // heuristic, its not worth it, as the strategy will typically explore thousands of iterations,
            // plus its also interesting to explore a schedule with no forced priority switch points.
            if (iteration > 0)
            {
                // FN_TODO: review an fix this code for new impl now
                string envPCTProbability = Environment.GetEnvironmentVariable("MYCOYOTE_PCT_PROB"); // NOTE: MYCOYOTE_NEW_PCT must be a either 0 or 1.
                bool envPCTProbabilityBool = false;
                if (envPCTProbability != null)
                {
                    envPCTProbabilityBool = bool.Parse(envPCTProbability);
                }

                if (envPCTProbabilityBool)
                {
                    Console.WriteLine();
                    Console.WriteLine($"        <PCTLog> Iteration : {iteration}");
                    int n = this.PrioritizedOperations.Count; // FN_TODO
                    // int n = this.AllRegisteredOperations.Count; // FN_TODO
                    int d = this.MaxPrioritySwitchPoints;
                    int k = this.StepCount;
                    double power = Math.Pow(k, d - 1);
                    double denominator = n * power;
                    double theoreticalProbability = 1 / denominator;
                    // Console.WriteLine($"        <PCTLog> power = {power}, denominator = {denominator}");
                    // Console.WriteLine($"        <PCTLog> this.PrioritizedOperations.Count: N = {n}");
                    // Console.WriteLine($"        <PCTLog> this.MaxPrioritySwitchPoints: D = {d}");
                    // Console.WriteLine($"        <PCTLog> this.StepCount: K = {k}");
                    Console.WriteLine($"        <PCTLog> N = {n}, D = {d}, K = {k}");
                    Console.WriteLine($"        <PCTLog> Theoretical-Probability : {theoreticalProbability}");
                    Console.WriteLine();
                }

                this.ScheduleLength = Math.Max(this.ScheduleLength, this.StepCount);
                this.StepCount = 0;

                // FN_TODO: properly clean these datastructures?
                this.AsyncOperationToOperationsGroupMap.Clear();
                this.NonAsyncStateMachineOperationGroup = null;
                this.PrioritizedOperations.Clear();
                this.AllRegisteredOperations.Clear();
                this.RegisteredOps.Clear();

                this.ContextSwitchNumber = 0;
                this.ContextSwitchNumber = 0;
                // this.ActualNumberOfPrioritySwitches = 0;

                this.PriorityChangePoints.Clear();

                var range = Enumerable.Range(0, this.ScheduleLength);
                foreach (int point in this.Shuffle(range).Take(this.MaxPrioritySwitchPoints))
                {
                    this.PriorityChangePoints.Add(point);
                }

                this.DebugPrintPriorityChangePoints();
            }

            return true;
        }

        private void DebugPrintBeforeGetNextOperation(IEnumerable<AsyncOperation> opss)
        {
            this.ContextSwitchNumber += 1;
            var ops = opss.ToList();
            Console.WriteLine($"          ops.Count = {ops.Count}");
            int countt = 0;
            foreach (var op in ops)
            {
                if (countt == 0)
                {
                    Console.Write($"          {op}");
                }
                else
                {
                    Console.Write($", {op}");
                }

                countt++;
            }

            Console.WriteLine();

            countt = 0;
            foreach (var op in ops)
            {
                if (countt == 0)
                {
                    Console.Write($"          {op.Status}");
                }
                else
                {
                    Console.Write($", {op.Status}");
                }

                countt++;
            }

            Console.WriteLine();

            countt = 0;
            foreach (var op in ops)
            {
                if (countt == 0)
                {
                    Console.Write($"          {op.Type}");
                }
                else
                {
                    Console.Write($", {op.Type}");
                }

                countt++;
            }

            Console.WriteLine();

            HashSet<AsyncOperation> newConcurrentOps = new HashSet<AsyncOperation>();
            foreach (var op in ops)
            {
                if (!this.RegisteredOps.Contains(op))
                {
                    newConcurrentOps.Add(op);
                    this.RegisteredOps.Add(op);
                }
            }

            Console.WriteLine($"          # new operations added {newConcurrentOps.Count}");
            Specification.Assert((newConcurrentOps.Count <= 1) || (newConcurrentOps.Count == 2 && this.ContextSwitchNumber == 1),
                $"     <TaskSummaryLog-ERROR> At most one new operation must be added across context switch.");

            int cases = 0;

            if (newConcurrentOps.Count == 0)
            {
                Console.WriteLine($"     <TaskSummaryLog> T-case 1.): No new task added.");
                cases = 1;
            }

            foreach (var op in newConcurrentOps)
            {
                Console.WriteLine($"          newConcurrentOps: {op}, Spawner: {op.ParentTask}");
                if (op.IsContinuationTask)
                {
                    if (op.ParentTask == null)
                    {
                        Console.WriteLine($"     <TaskSummaryLog> T-case 3.): Continuation task {op} (id = {op.Id}) is the first task to be created!");
                    }
                    else
                    {
                        Console.WriteLine($"     <TaskSummaryLog> T-case 3.): Continuation task {op} (id = {op.Id}) created by {op.ParentTask} (id = {op.ParentTask.Id}).");
                    }

                    cases = 3;
                }
                else
                {
                    if (op.ParentTask == null)
                    {
                        Console.WriteLine($"     <TaskSummaryLog> T-case 2.): Spawn task {op} (id = {op.Id}) is the first task to be created!");
                    }
                    else
                    {
                        Console.WriteLine($"     <TaskSummaryLog> T-case 2.): Spawn task {op} (id = {op.Id}) created by {op.ParentTask} (id = {op.ParentTask.Id}).");
                    }

                    cases = 2;
                }
            }

            Specification.Assert( (cases == 1) || (cases == 2) || (cases == 3),
                $"     <TaskSummaryLog-ERROR> At most one new operation must be added across context switch.");

            // Console.WriteLine();
        }

        private static void DebugPrintAfterGetNextOperation(AsyncOperation next)
        {
            Console.WriteLine($"          next = {next}");
            Console.WriteLine($"     <TaskSummaryLog> Scheduled: {next}");
            // Console.WriteLine();
            // Console.WriteLine();
            // Console.WriteLine();
            // Console.WriteLine();
            // Console.WriteLine();
        }

        /// <inheritdoc/>
        internal override bool GetNextOperation(IEnumerable<AsyncOperation> ops, AsyncOperation current,
            bool isYielding, out AsyncOperation next)
        {
            this.DebugPrintBeforeGetNextOperation(ops);
            next = null;
            var enabledOps = ops.Where(op => op.Status is AsyncOperationStatus.Enabled).ToList();
            if (enabledOps.Count is 0)
            {
                return false;
            }

            this.SetNewOperationPriorities(enabledOps, current);
            this.DeprioritizeEnabledOperationWithHighestPriority(enabledOps, current, isYielding);
            this.DebugPrintOperationPriorityList();
            DebugPrintEnabledOps(enabledOps);

            AsyncOperation highestEnabledOperation = this.GetEnabledOperationWithHighestPriority(enabledOps);
            next = enabledOps.First(op => op.Equals(highestEnabledOperation));
            Console.WriteLine("<PCTLog> next operation scheduled is: '{0}'.", next);
            this.StepCount++;

            // string envPrintEnabledChildrenDepth = Environment.GetEnvironmentVariable("MYCOYOTE_PRINT_MAX_ENABLED_CHILDS"); // NOTE: OLP_TEST_PCT_SWITCHES muse be a positive integer.
            // bool envPrintEnabledChildrenDepthBool = false;
            // if (envPrintEnabledChildrenDepth != null)
            // {
            //     envPrintEnabledChildrenDepthBool = bool.Parse(envPrintEnabledChildrenDepth);
            // }

            // if (envPrintEnabledChildrenDepthBool)
            // {
            //     this.DebugPrintMaxEnabledSpawneeDepth(enabledOps);
            // }

            this.DebugPrintBeforeGetNextOperation(ops);
            return true;
        }

        // FN_TODO: test and add capability to change the priority for parent and all its children as well also randomization inside the chains
        // private int GiveCorrectIndexAfterPrioritInheritanceForOperation(AsyncOperation operation)
        // {
        //     int index = 0;
        //     // int beginIndex = 0;
        //     // int endIndex = this.PrioritizedOperations.Count;
        //     // foreach (var opp in this.PrioritizedOperations)
        //     // {
        //     //     if (opp.ParentTask == operation.ParentTask || opp == operation.ParentTask)
        //     //     {
        //     //         beginIndex = this.PrioritizedOperations.IndexOf(opp);
        //     //         break;
        //     //     }
        //     // }

        // // foreach (var opp in this.PrioritizedOperations)
        //     // {
        //     //     if (opp.ParentTask == operation.ParentTask || opp == operation.ParentTask)
        //     //     {
        //     //         endIndex = this.PrioritizedOperations.IndexOf(opp);
        //     //     }
        //     // }

        // // if (beginIndex == endIndex)
        //     // {
        //     //     index = beginIndex + this.RandomValueGenerator.Next(1);
        //     // }
        //     // else
        //     // {
        //     //     index = beginIndex + this.RandomValueGenerator.Next(endIndex - beginIndex + 1); // FN_TODO: maybe fix the bug
        //     // }

        // // if (index < 0)
        //     // {
        //     //     index = 0;
        //     // }

        // // if (index > this.PrioritizedOperations.Count)
        //     // {
        //     //     index = this.PrioritizedOperations.Count;
        //     // }

        // index = this.PrioritizedOperations.IndexOf(operation.ParentTask) + 1;
        //     // index = this.PrioritizedOperations.IndexOf(op.Spawner) + 1;
        //     return index;
        // }

        // private void fixUnhandledMoveNextPriorities()
        // {
        //     // string envNewPCT = Environment.GetEnvironmentVariable("MYCOYOTE_NEW_PCT"); // NOTE: MYCOYOTE_NEW_PCT must be a either 0 or 1.
        //     // bool envNewPCTBool = false;
        //     // if (envNewPCT != null)
        //     // {
        //     //     envNewPCTBool = bool.Parse(envNewPCT);
        //     // }

        // // Fix the priorities of all the operations which have an unhandled MoveNext call
        //     foreach (var op in this.AllRegisteredOperations.Where(op => !op.LastMoveNextHandled))
        //     {
        //         this.InsertAsyncOperationIntoOperationGroup(op);
        //         // if (envNewPCTBool)
        //         // {
        //             // int index = GiveCorrectIndexAfterPrioritInheritanceForOperation(op);
        //             // op.lastMoveNextPriorityAdjusted = true;
        //             // this.PrioritizedOperations.Insert(index, op);
        //             // Console.WriteLine("<PCTLog> Adjusted priority to '{0}' for operation '{1}' after calling a MoveNext.", index, op.Name);
        //             // if (op.IsContinuationTask)
        //             // {
        //             //     IO.Debug.WriteLine($"===========<IMP_PCTStrategy> [SetNewOperationPriorities] CASE3: priority of OLD continuation task: {op} is adjusted to: {index}");
        //             // }
        //             // else
        //             // {
        //             //     IO.Debug.WriteLine($"===========<IMP_PCTStrategy> [SetNewOperationPriorities] CASE3: priority of OLD spawn task: {op} is adjusted to: {index}");
        //             // }
        //         // }

        // }
        // }

        // FN_TODO: put more assertions to cover corner cases if possible
        private void InsertAsyncOperationIntoOperationGroup(AsyncOperation asyncOp)
        {
            if (asyncOp.TaskGroupID == -1)
            {
                if (this.NonAsyncStateMachineOperationGroup == null)
                {
                    this.NonAsyncStateMachineOperationGroup = new AsyncStateMachineTaskOperationsGroup(asyncOp, this.RandomValueGenerator);
                    int index = this.RandomValueGenerator.Next(this.PrioritizedOperations.Count) + 1;
                    this.PrioritizedOperations.Insert(index, this.NonAsyncStateMachineOperationGroup);
                    this.NonAsyncStateMachineOperationGroup.InsertOperation(asyncOp);
                    this.AsyncOperationToOperationsGroupMap.Add(asyncOp, this.NonAsyncStateMachineOperationGroup);
                    Debug.WriteLine($"===========<IMP_PCTStrategy> [InsertAsyncOperationIntoOperationGroup] CASE_1: inserted owner asyncOp: {asyncOp} into new NonAsyncStateMachineOperationGroup.");
                }
                else
                {
                    this.NonAsyncStateMachineOperationGroup.InsertOperation(asyncOp);
                    this.AsyncOperationToOperationsGroupMap.Add(asyncOp, this.NonAsyncStateMachineOperationGroup);
                    Debug.WriteLine($"===========<IMP_PCTStrategy> [InsertAsyncOperationIntoOperationGroup] CASE_2: inserted owner asyncOp: {asyncOp} into old NonAsyncStateMachineOperationGroup.");
                }
            }
            else
            {
                // FN_TODO_2: Put appropriate assertions
                if (asyncOp.IsOwnerSpawnOperation)
                {
                    AsyncStateMachineTaskOperationsGroup newOperationGroup = new AsyncStateMachineTaskOperationsGroup(asyncOp, this.RandomValueGenerator);
                    int index = this.RandomValueGenerator.Next(this.PrioritizedOperations.Count) + 1;
                    this.PrioritizedOperations.Insert(index, newOperationGroup);
                    this.AsyncOperationToOperationsGroupMap.Add(asyncOp, newOperationGroup);
                    // asyncOp.IsOwnerSpawnOperation = true;
                    Debug.WriteLine($"===========<IMP_PCTStrategy> [InsertAsyncOperationIntoOperationGroup] CASE_3: inserted owner asyncOp: {asyncOp} into a new chain.");
                }
                else if (!asyncOp.LastMoveNextHandled)
                {
                    Specification.Assert(asyncOp.ParentTask != null, $"     ===========<IMP_PCTStrategy-ERROR> [InsertAsyncOperationIntoOperationGroup] asyncOp.ParentTask != null.");
                    this.AsyncOperationToOperationsGroupMap[asyncOp].RemoveOperation(asyncOp);
                    AsyncOperation oldChainOwner = this.AsyncOperationToOperationsGroupMap[asyncOp].GetOwnerOperation(); // for DEBUGGING
                    this.AsyncOperationToOperationsGroupMap[asyncOp.ParentTask].InsertOperation(asyncOp);
                    AsyncOperation newChainOwner = this.AsyncOperationToOperationsGroupMap[asyncOp.ParentTask].GetOwnerOperation(); // for DEBUGGING
                    this.AsyncOperationToOperationsGroupMap.Add(asyncOp, this.AsyncOperationToOperationsGroupMap[asyncOp.ParentTask]);
                    asyncOp.LastMoveNextHandled = true;
                    Debug.WriteLine($"===========<IMP_PCTStrategy> [InsertAsyncOperationIntoOperationGroup] CASE_3: moved asyncOp: {asyncOp} from old chain of {oldChainOwner} to new chain of {newChainOwner}.");
                }
                else
                {
                    Specification.Assert(false, $"     ===========<IMP_PCTStrategy-ERROR> [InsertAsyncOperationIntoOperationGroup] unreachable CASE_4 touched.");
                }
            }
        }

        /// <summary>
        /// Sets the priority of new operations, if there are any.
        /// </summary>
        private void SetNewOperationPriorities(List<AsyncOperation> ops, AsyncOperation current)
        {
            if (this.AllRegisteredOperations.Count is 0)
            {
                this.InsertAsyncOperationIntoOperationGroup(current);
                this.AllRegisteredOperations.Add(current);
            }

            // string envNewPCT = Environment.GetEnvironmentVariable("MYCOYOTE_NEW_PCT"); // NOTE: MYCOYOTE_NEW_PCT must be a either 0 or 1.
            // bool envNewPCTBool = false;
            // if (envNewPCT != null)
            // {
            //     envNewPCTBool = bool.Parse(envNewPCT);
            // }

            // Randomize the priority of all new operations.
            foreach (var op in ops.Where(op => !this.AllRegisteredOperations.Contains(op)))
            {
                // int index = 0;
                // if (!envNewPCTBool)
                // {
                //     index = this.RandomValueGenerator.Next(this.PrioritizedOperations.Count) + 1;
                // }
                // else
                // {
                //     // FN_TODO: think of cases with mix of tasks, actors, threads, etc where op might not be be aof type TaskOperation
                //     if (op.lastMoveNextPriorityAdjusted)
                //     {
                //         // Randomly choose a priority for this operation since it is a new operation which does not have unhandled MoveNext call
                //         index = this.RandomValueGenerator.Next(this.PrioritizedOperations.Count) + 1;
                //         if (op.IsContinuationTask)
                //         {
                //             IO.Debug.WriteLine($"===========<IMP_PCTStrategy> [SetNewOperationPriorities] CASE1: priority of NEW continuation task: {op} is set to: {index}");
                //         }
                //         else
                //         {
                //             IO.Debug.WriteLine($"===========<IMP_PCTStrategy> [SetNewOperationPriorities] CASE1: priority of NEW spawn task: {op} is set to: {index}");
                //         }
                //     }
                //     else
                //     {
                //         index = GiveCorrectIndexAfterPrioritInheritanceForOperation(op);
                //         op.lastMoveNextPriorityAdjusted = true;
                //         if (op.IsContinuationTask)
                //         {
                //             IO.Debug.WriteLine($"===========<IMP_PCTStrategy> [SetNewOperationPriorities] CASE2: priority of NEW continuation task: {op} is adjusted to: {index}");
                //         }
                //         else
                //         {
                //             IO.Debug.WriteLine($"===========<IMP_PCTStrategy> [SetNewOperationPriorities] CASE2: priority of NEW spawn task: {op} is adjusted to: {index}");
                //         }
                //     }
                // }

                // this.PrioritizedOperations.Insert(index, op);
                // Console.WriteLine("<PCTLog> chose priority '{0}' for new operation '{1}'.", index, op.Name);

                this.AllRegisteredOperations.Add(op);
                if (op.IsContinuationTask)
                {
                    Debug.WriteLine($"===========<IMP_PCTStrategy> [SetNewOperationPriorities] handling NEW continuation task: {op}.");
                }
                else
                {
                    Debug.WriteLine($"===========<IMP_PCTStrategy> [SetNewOperationPriorities] handling NEW spawn task: {op}.");
                }

                this.InsertAsyncOperationIntoOperationGroup(op);
            }

            foreach (var op in this.AllRegisteredOperations.Where(op => !op.LastMoveNextHandled))
            {
                if (op.IsContinuationTask)
                {
                    Debug.WriteLine($"===========<IMP_PCTStrategy> [SetNewOperationPriorities] changing priority due to MoveNext of continuation task: {op}.");
                }
                else
                {
                    Debug.WriteLine($"===========<IMP_PCTStrategy> [SetNewOperationPriorities] changing priority due to MoveNext of spawn task: {op}.");
                }

                this.InsertAsyncOperationIntoOperationGroup(op);
            }

            // fixUnhandledMoveNextPriorities();
        }

        // private void DebugPrintMaxEnabledSpawneeDepth(List<AsyncOperation> enabledOps)
        // {
        //     foreach (var op in this.PrioritizedOperations)
        //     {
        //         int enabledSpawneesCount = 0;
        //         foreach (var spawnee in op.Spawnees.Where(spawnee => enabledOps.Contains(spawnee)))
        //         {
        //             enabledSpawneesCount++;
        //         }

        // if (this.EnabledSpawneesCountMax < enabledSpawneesCount)
        //         {
        //             this.EnabledSpawneesCountMax = enabledSpawneesCount;
        //             string folder = @"C:\Users\t-fnayyar\Desktop\repos\olp-buggy-branches\";
        //             string fileName = "maxEnabledChildDepth.txt";
        //             string fullPath = folder + fileName;
        //             File.WriteAllText(fullPath, $"{this.EnabledSpawneesCountMax} \n");
        //         }
        //     }
        // }

        /// <summary>
        /// Deprioritizes the enabled operation with the highest priority, if there is a
        /// priotity change point installed on the current execution step.
        /// </summary>
        private void DeprioritizeEnabledOperationWithHighestPriority(List<AsyncOperation> ops, AsyncOperation current, bool isYielding)
        {
            if (ops.Count <= 1)
            {
                // Nothing to do, there is only one enabled operation available.
                return;
            }

            AsyncOperation deprioritizedOperation = null;
            if (this.PriorityChangePoints.Contains(this.StepCount))
            {
                // This scheduling step was chosen as a priority switch point.
                deprioritizedOperation = this.GetEnabledOperationWithHighestPriority(ops);
                // Debug.WriteLine("<PCTLog> operation '{0}' is deprioritized.", deprioritizedOperation.Name);
                Debug.WriteLine($"<PCTLog> operationGroup of owner op: {this.AsyncOperationToOperationsGroupMap[deprioritizedOperation].GetOwnerOperation()} is deprioritized.");
            }
            else if (isYielding)
            {
                // The current operation is yielding its execution to the next prioritized operation.
                deprioritizedOperation = current;
                // Debug.WriteLine("<PCTLog> operation '{0}' yields its priority.", deprioritizedOperation.Name);
                Debug.WriteLine($"<PCTLog> operationGroup of owner op: {this.AsyncOperationToOperationsGroupMap[deprioritizedOperation].GetOwnerOperation()} yields its priority..");
            }

            if (deprioritizedOperation != null)
            {
                // string envNewPCT = Environment.GetEnvironmentVariable("MYCOYOTE_NEW_PCT"); // NOTE: MYCOYOTE_NEW_PCT must be a either 0 or 1.
                // bool envNewPCTBool = false;
                // if (envNewPCT != null)
                // {
                //     envNewPCTBool = bool.Parse(envNewPCT);
                // }

                // if (!envNewPCTBool)
                // {
                    // Deprioritize the operation by putting it in the end of the list.
                this.PrioritizedOperations.Remove(this.AsyncOperationToOperationsGroupMap[deprioritizedOperation]);
                this.PrioritizedOperations.Add(this.AsyncOperationToOperationsGroupMap[deprioritizedOperation]);
                // }
                // else
                // {
                //     string envPrintNumOfPrioritySwtichPoints = Environment.GetEnvironmentVariable("MYCOYOTE_PRINT_PRIORITY_SWITCHES"); // NOTE: OLP_TEST_PCT_SWITCHES muse be a positive integer.
                //     bool envPrintNumOfPrioritySwtichPointsBool = false;
                //     if (envPrintNumOfPrioritySwtichPoints != null)
                //     {
                //         envPrintNumOfPrioritySwtichPointsBool = bool.Parse(envPrintNumOfPrioritySwtichPoints);
                //     }

                // if (envPrintNumOfPrioritySwtichPointsBool)
                //     {
                //         this.ActualNumberOfPrioritySwitches++;
                //         string folder = @"C:\Users\t-fnayyar\Desktop\repos\olp-buggy-branches\";
                //         string fileName = "ActualNumberOfPrioritySwitches.txt";
                //         string fullPath = folder + fileName;
                //         File.WriteAllText(fullPath, $"{this.ActualNumberOfPrioritySwitches} \n");
                //     }

                // // Deprioritize the operation by putting it in the end of the list.

                // /*this.PrioritizedOperations.Remove(deprioritizedOperation);
                //     this.PrioritizedOperations.Add(deprioritizedOperation);
                //     foreach (AsyncOperation spawnee in deprioritizedOperation.Spawnees)
                //     {
                //         this.PrioritizedOperations.Remove(spawnee);
                //         this.PrioritizedOperations.Add(spawnee);
                //     }*/

                // LinkedList<AsyncOperation> toDeprioratize = new LinkedList<AsyncOperation>();
                //     foreach (AsyncOperation aop in this.PrioritizedOperations)
                //     {
                //         // deprioratize all the children (spanwnees), siblings and parent.
                //         if (aop == deprioritizedOperation || deprioritizedOperation.Spawnees.Contains(aop) || aop.ParentTask == deprioritizedOperation.ParentTask || aop == deprioritizedOperation.ParentTask)
                //         {
                //             toDeprioratize.AddLast(aop);
                //         }
                //     }

                // foreach (AsyncOperation aop in toDeprioratize)
                //     {
                //         this.PrioritizedOperations.Remove(aop);
                //         this.PrioritizedOperations.Add(aop);
                //     }
                // }
            }
        }

        /// <summary>
        /// Returns the enabled operation with the highest priority.
        /// </summary>
        private AsyncOperation GetEnabledOperationWithHighestPriority(List<AsyncOperation> ops)
        {
            foreach (var operationGroup in this.PrioritizedOperations)
            {
                List<AsyncOperation> operationChain = operationGroup.GetOperationsChain();
                foreach (var entity in operationChain)
                {
                    if (ops.Any(m => m == entity))
                    {
                        return operationGroup.GiveRandomEnabledOperation();
                    }
                }
            }

            return null;
        }

        /// <inheritdoc/>
        internal override bool GetNextBooleanChoice(AsyncOperation current, int maxValue, out bool next)
        {
            next = false;
            if (this.RandomValueGenerator.Next(maxValue) is 0)
            {
                next = true;
            }

            this.StepCount++;
            return true;
        }

        /// <inheritdoc/>
        internal override bool GetNextIntegerChoice(AsyncOperation current, int maxValue, out int next)
        {
            next = this.RandomValueGenerator.Next(maxValue);
            this.StepCount++;
            return true;
        }

        /// <inheritdoc/>
        internal override int GetStepCount() => this.StepCount;

        /// <inheritdoc/>
        internal override bool IsMaxStepsReached()
        {
            if (this.MaxSteps is 0)
            {
                return false;
            }

            return this.StepCount >= this.MaxSteps;
        }

        /// <inheritdoc/>
        internal override bool IsFair() => false;

        /// <inheritdoc/>
        internal override string GetDescription()
        {
            var text = $"pct[seed '" + this.RandomValueGenerator.Seed + "']";
            return text;
        }

        /// <summary>
        /// Shuffles the specified range using the Fisher-Yates algorithm.
        /// </summary>
        /// <remarks>
        /// See https://en.wikipedia.org/wiki/Fisher%E2%80%93Yates_shuffle.
        /// </remarks>
        private IList<int> Shuffle(IEnumerable<int> range)
        {
            var result = new List<int>(range);
            for (int idx = result.Count - 1; idx >= 1; idx--)
            {
                int point = this.RandomValueGenerator.Next(result.Count);
                int temp = result[idx];
                result[idx] = result[point];
                result[point] = temp;
            }

            return result;
        }

        /// <inheritdoc/>
        internal override void Reset()
        {
            this.ScheduleLength = 0;
            this.StepCount = 0;
            this.PrioritizedOperations.Clear();
            this.PriorityChangePoints.Clear();
        }

        private static void DebugPrintEnabledOps(List<AsyncOperation> ops)
        {
            if (Debug.IsEnabled)
            {
                Debug.Write("<PCTLog> enabled operation: ");
                for (int idx = 0; idx < ops.Count; idx++)
                {
                    if (idx < ops.Count - 1)
                    {
                        Debug.Write("'{0}', ", ops[idx].Name);
                    }
                    else
                    {
                        Debug.WriteLine("'{0}'.", ops[idx].Name);
                    }
                }
            }
        }

        /// <summary>
        /// Print the operation priority list, if debug is enabled.
        /// </summary>
        private void DebugPrintOperationPriorityList()
        {
            if (Debug.IsEnabled)
            {
                Debug.Write("<PCTLog> operation priority list: ");
                for (int idx = 0; idx < this.PrioritizedOperations.Count; idx++)
                {
                    Debug.Write($"<PCTLog> chain_{idx}: ");
                    List<AsyncOperation> operationChain = this.PrioritizedOperations[idx].GetOperationsChain();
                    for (int jdx = 0; jdx < operationChain.Count; jdx++)
                    {
                        if (jdx < operationChain.Count - 1)
                        {
                            Debug.Write("'{0}', ", operationChain[jdx].Name);
                        }
                        else
                        {
                            Debug.WriteLine("'{0}'.", operationChain[jdx].Name);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Print the priority change points, if debug is enabled.
        /// </summary>
        private void DebugPrintPriorityChangePoints()
        {
            if (Debug.IsEnabled)
            {
                // Sort them before printing for readability.
                var sortedChangePoints = this.PriorityChangePoints.ToArray();
                Array.Sort(sortedChangePoints);
                Debug.WriteLine("<PCTLog> next priority change points ('{0}' in total): {1}",
                    sortedChangePoints.Length, string.Join(", ", sortedChangePoints));
            }
        }
    }
}
