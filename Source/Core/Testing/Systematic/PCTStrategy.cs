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

            internal int GetTaskGroupID()
            {
                return this.TaskGroupID;
            }

            internal AsyncOperation GetOwnerOperation()
            {
                return this.OwnerOperation;
            }

            internal void InsertOperation(AsyncOperation newOperation)
            {
                // FN_TODO: think, is this randomization required?
                if (this.OperationsChain.Count == 0)
                {
                    this.OperationsChain.Add(newOperation);
                }
                else
                {
                    int index = this.RandomValueGenerator.Next(this.OperationsChain.Count + 1);
                    this.OperationsChain.Insert(index, newOperation);
                }
            }

            internal void RemoveOperation(AsyncOperation operationToRemove)
            {
                // FN_TODO: add logic to prevent removing of OwnerOperation. OwnerOperation is always present in the operation chain.
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

        private int ContextSwitchNumber = 0;

        private int ActualNumberOfPrioritySwitches = 0;

        private readonly Dictionary<AsyncOperation, AsyncStateMachineTaskOperationsGroup> AsyncOperationToOperationsGroupMap;

        private AsyncStateMachineTaskOperationsGroup NonAsyncStateMachineOperationGroup;

        private AsyncStateMachineTaskOperationsGroup DelayOperationGroup;

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
            this.ActualNumberOfPrioritySwitches = 0;
            this.AsyncOperationToOperationsGroupMap = new Dictionary<AsyncOperation, AsyncStateMachineTaskOperationsGroup>();
            // this.NonAsyncStateMachineOperationGroup = new AsyncStateMachineTaskOperationsGroup();
            // this.DelayOperationGroup = new AsyncStateMachineTaskOperationsGroup();
            this.PrioritizedOperations = new List<AsyncStateMachineTaskOperationsGroup>();
            this.AllRegisteredOperations = new List<AsyncOperation>();
            this.RegisteredOps = new HashSet<AsyncOperation>();
        }

        internal void PrintTaskPCTStatsForIteration(uint iteration)
        {
            Debug.WriteLine(string.Empty);
            Debug.WriteLine($"===========<IMP_PCTStrategy> [PrintTaskPCTStatsForIteration] TASK-PCT STATS for ITERATION: {iteration}");
            Debug.WriteLine($"                  TOTAL ASYNC OPS: {this.AllRegisteredOperations.Count}");
            Debug.WriteLine($"                  TOTAL ASYNC OP GROUPS (#PRIORITIES): {this.PrioritizedOperations.Count}");
            Debug.WriteLine($"                  #PRIORITY_SWITCHES: {this.ActualNumberOfPrioritySwitches}");
            this.DebugPrintOperationPriorityList();
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
                // FN_TODO: print the stat for the last iteration also
                this.PrintTaskPCTStatsForIteration(iteration - 1);

                // FN_TODO: review an fix this code for new impl now
               /* string envPCTProbability = Environment.GetEnvironmentVariable("MYCOYOTE_PCT_PROB"); // NOTE: MYCOYOTE_NEW_PCT must be a either 0 or 1.
                bool envPCTProbabilityBool = false;
                if (envPCTProbability != null)
                {
                    envPCTProbabilityBool = bool.Parse(envPCTProbability);
                }*/

                /*if (envPCTProbabilityBool)
                {*/
                Debug.WriteLine(string.Empty);
                Debug.WriteLine($"===========<IMP_PCTStrategy> PROBABILITY STATS for ITERATION: {iteration - 1}");

                int n_theory = this.AllRegisteredOperations.Count; // FN_TODO : think are we measuring corrent parameters for probabilities?
                int d_theory = Math.Max(this.MaxPrioritySwitchPoints, 1);
                int k_theory = this.StepCount;
                double power_theory = Math.Pow(k_theory, d_theory - 1);
                double denominator_theory = n_theory * power_theory;
                double theoreticalProbability_theory = 1 / denominator_theory;
                Debug.WriteLine($"                    N_th = {n_theory}, D_th = {d_theory}, K_th = {k_theory}");
                Debug.WriteLine($"                    Theoretical-Probability : {theoreticalProbability_theory}");
                Debug.WriteLine(string.Empty);

                int n_actual = this.PrioritizedOperations.Count; // FN_TODO : think are we measuring corrent parameters for probabilities?
                // int d_actual = Math.Max(this.ActualNumberOfPrioritySwitches, 1);
                int d_actual = d_theory;
                int k_actual = k_theory;
                double power_actual = Math.Pow(k_actual, d_actual - 1);
                double denominator_actual = n_actual * power_actual;
                double theoreticalProbability_actual = 1 / denominator_actual;
                Debug.WriteLine($"                  N_pr = {n_actual}, D_pr = {d_actual}, K_pr = {k_actual}");
                Debug.WriteLine($"                    Practical-Probability : {theoreticalProbability_actual}");
                Debug.WriteLine(string.Empty);
                /*}*/

                this.ScheduleLength = Math.Max(this.ScheduleLength, this.StepCount);
                this.StepCount = 0;

                // FN_TODO: properly clean these datastructures?
                this.AsyncOperationToOperationsGroupMap.Clear();
                this.NonAsyncStateMachineOperationGroup = null;
                this.DelayOperationGroup = null;
                this.PrioritizedOperations.Clear();
                this.AllRegisteredOperations.Clear();
                this.RegisteredOps.Clear();

                this.ContextSwitchNumber = 0;
                this.ActualNumberOfPrioritySwitches = 0;

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
            Debug.WriteLine($"          ops.Count = {ops.Count}");
            int countt = 0;
            foreach (var op in ops)
            {
                if (countt == 0)
                {
                    Debug.Write($"          {op}");
                }
                else
                {
                    Debug.Write($", {op}");
                }

                countt++;
            }

            Debug.WriteLine(string.Empty);

            countt = 0;
            foreach (var op in ops)
            {
                if (countt == 0)
                {
                    Debug.Write($"          {op.Status}");
                }
                else
                {
                    Debug.Write($", {op.Status}");
                }

                countt++;
            }

            Debug.WriteLine(string.Empty);

            countt = 0;
            foreach (var op in ops)
            {
                if (countt == 0)
                {
                    Debug.Write($"          {op.Type}");
                }
                else
                {
                    Debug.Write($", {op.Type}");
                }

                countt++;
            }

            Debug.WriteLine(string.Empty);

            HashSet<AsyncOperation> newConcurrentOps = new HashSet<AsyncOperation>();
            foreach (var op in ops)
            {
                if (!this.RegisteredOps.Contains(op))
                {
                    newConcurrentOps.Add(op);
                    this.RegisteredOps.Add(op);
                }
            }

            Debug.WriteLine($"          # new operations added {newConcurrentOps.Count}");
            Specification.Assert((newConcurrentOps.Count <= 1) || (newConcurrentOps.Count == 2 && this.ContextSwitchNumber == 1),
                $"     <TaskSummaryLog-ERROR> At most one new operation must be added across context switch.");

            int cases = 0;

            if (newConcurrentOps.Count == 0)
            {
                Debug.WriteLine($"     <TaskSummaryLog> T-case 1.): No new task added.");
                cases = 1;
            }

            foreach (var op in newConcurrentOps)
            {
                Debug.WriteLine($"          newConcurrentOps: {op}, Spawner: {op.ParentTask}");
                if (op.IsContinuationTask)
                {
                    if (op.ParentTask == null)
                    {
                        Debug.WriteLine($"     <TaskSummaryLog> T-case 3.): Continuation task {op} (id = {op.Id}) is the first task to be created!");
                    }
                    else
                    {
                        Debug.WriteLine($"     <TaskSummaryLog> T-case 3.): Continuation task {op} (id = {op.Id}) created by {op.ParentTask} (id = {op.ParentTask.Id}).");
                    }

                    cases = 3;
                }
                else
                {
                    if (op.ParentTask == null)
                    {
                        Debug.WriteLine($"     <TaskSummaryLog> T-case 2.): Spawn task {op} (id = {op.Id}) is the first task to be created!");
                    }
                    else
                    {
                        Debug.WriteLine($"     <TaskSummaryLog> T-case 2.): Spawn task {op} (id = {op.Id}) created by {op.ParentTask} (id = {op.ParentTask.Id}).");
                    }

                    cases = 2;
                }
            }

            Specification.Assert( (cases == 1) || (cases == 2) || (cases == 3),
                $"     <TaskSummaryLog-ERROR> At most one new operation must be added across context switch.");

            // Debug.WriteLine();
        }

        private static void DebugPrintAfterGetNextOperation(AsyncOperation next)
        {
            Debug.WriteLine($"          next = {next}");
            Debug.WriteLine($"     <TaskSummaryLog> Scheduled: {next}");
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
            Debug.WriteLine("<PCTLog> next operation scheduled is: '{0}'.", next);
            Debug.WriteLine($"===========<IMP_PCTStrategy> [GetNextOperation] NEXT operation to be scheduled: {next}.");
            this.StepCount++;
            this.DebugPrintBeforeGetNextOperation(ops);
            return true;
        }

        // FN_TODO: put more assertions to cover corner cases if possible
        private void InsertNewAsyncOperationIntoOperationGroup(AsyncOperation asyncOp)
        {
            // FN_IMP_TODO: operations in the this.NonAsyncStateMachineOperationGroup can also get different priorities and be deprioratized.
            // FN_IMP_TODO: Modularize the code, lot of code duplication.
            if (asyncOp.TaskGroupID == -1)
            {
                if (asyncOp.IsDelayTaskOperation)
                {
                    if (this.DelayOperationGroup == null)
                    {
                        this.DelayOperationGroup = new AsyncStateMachineTaskOperationsGroup(asyncOp, this.RandomValueGenerator);
                        if (this.PrioritizedOperations.Count == 0)
                        {
                            this.PrioritizedOperations.Add(this.DelayOperationGroup);
                            Debug.WriteLine($"===========<IMP_PCTStrategy> [InsertAsyncOperationIntoOperationGroup] CASE_4: inserted [FIRST] delay asyncOp: {asyncOp} into new DelayOperationGroup with priority: 0");
                        }
                        else
                        {
                            int index = this.RandomValueGenerator.Next(this.PrioritizedOperations.Count + 1);
                            this.PrioritizedOperations.Insert(index, this.DelayOperationGroup);
                            Debug.WriteLine($"===========<IMP_PCTStrategy> [InsertAsyncOperationIntoOperationGroup] CASE_4: inserted delay asyncOp: {asyncOp} into new DelayOperationGroup with priority: {index}");
                        }

                        this.AsyncOperationToOperationsGroupMap.Add(asyncOp, this.DelayOperationGroup);
                    }
                    else
                    {
                        this.DelayOperationGroup.InsertOperation(asyncOp);
                        this.AsyncOperationToOperationsGroupMap.Add(asyncOp, this.DelayOperationGroup);
                        Debug.WriteLine($"===========<IMP_PCTStrategy> [InsertAsyncOperationIntoOperationGroup] CASE_4: inserted delay asyncOp: {asyncOp} into old DelayOperationGroup which has priority: {this.PrioritizedOperations.IndexOf(this.DelayOperationGroup)}");
                    }
                }
                else
                {
                    if (this.NonAsyncStateMachineOperationGroup == null)
                    {
                        this.NonAsyncStateMachineOperationGroup = new AsyncStateMachineTaskOperationsGroup(asyncOp, this.RandomValueGenerator);
                        if (this.PrioritizedOperations.Count == 0)
                        {
                            this.PrioritizedOperations.Add(this.NonAsyncStateMachineOperationGroup);
                            Debug.WriteLine($"===========<IMP_PCTStrategy> [InsertAsyncOperationIntoOperationGroup] CASE_3: inserted [FIRST] delay/non-task asyncOp: {asyncOp} into new NonAsyncStateMachineOperationGroup with priority: 0");
                        }
                        else
                        {
                            int index = this.RandomValueGenerator.Next(this.PrioritizedOperations.Count + 1);
                            this.PrioritizedOperations.Insert(index, this.NonAsyncStateMachineOperationGroup);
                            Debug.WriteLine($"===========<IMP_PCTStrategy> [InsertAsyncOperationIntoOperationGroup] CASE_3: inserted delay/non-task asyncOp: {asyncOp} into new NonAsyncStateMachineOperationGroup with priority: {index}");
                        }

                        this.AsyncOperationToOperationsGroupMap.Add(asyncOp, this.NonAsyncStateMachineOperationGroup);
                    }
                    else
                    {
                        this.NonAsyncStateMachineOperationGroup.InsertOperation(asyncOp);
                        this.AsyncOperationToOperationsGroupMap.Add(asyncOp, this.NonAsyncStateMachineOperationGroup);
                        Debug.WriteLine($"===========<IMP_PCTStrategy> [InsertAsyncOperationIntoOperationGroup] CASE_3: inserted delay/non-task owner asyncOp: {asyncOp} into old NonAsyncStateMachineOperationGroup which has priority: {this.PrioritizedOperations.IndexOf(this.NonAsyncStateMachineOperationGroup)}");
                    }
                }
            }
            else
            {
                // FN_TODO_2: Put appropriate assertions
                if (asyncOp.IsOwnerSpawnOperation)
                {
                    AsyncStateMachineTaskOperationsGroup newOperationGroup = new AsyncStateMachineTaskOperationsGroup(asyncOp, this.RandomValueGenerator);
                    if (this.PrioritizedOperations.Count == 0)
                    {
                        this.PrioritizedOperations.Add(newOperationGroup);
                        Debug.WriteLine($"===========<IMP_PCTStrategy> [InsertAsyncOperationIntoOperationGroup] CASE_1: inserted [FIRST] owner asyncOp: {asyncOp} into a new chain with priority: 0");
                    }
                    else
                    {
                        int index = this.RandomValueGenerator.Next(this.PrioritizedOperations.Count + 1);
                        this.PrioritizedOperations.Insert(index, newOperationGroup);
                        Debug.WriteLine($"===========<IMP_PCTStrategy> [InsertAsyncOperationIntoOperationGroup] CASE_1: inserted owner asyncOp: {asyncOp} into a new chain with priority: {index}");
                    }

                    this.AsyncOperationToOperationsGroupMap.Add(asyncOp, newOperationGroup);
                    // asyncOp.IsOwnerSpawnOperation = true;
                }
                else
                {
                    Specification.Assert(false, $"     ===========<IMP_PCTStrategy-ERROR> [InsertAsyncOperationIntoOperationGroup] unreachable CASE_4 touched.");
                }
            }

            // FN_TODO: maybe later comment it to reduce log size.
            this.DebugPrintOperationPriorityList();
        }

        private void InsertAsyncOperationIntoOperationGroupOnMoveNext(AsyncOperation asyncOp)
        {
            Specification.Assert(asyncOp.ParentTask != null, $"     ===========<IMP_PCTStrategy-ERROR> [InsertAsyncOperationIntoOperationGroup] asyncOp.ParentTask != null.");
            Specification.Assert(this.AsyncOperationToOperationsGroupMap.ContainsKey(asyncOp), $"     ===========<IMP_PCTStrategy-ERROR> [InsertAsyncOperationIntoOperationGroup] on a MoveNext, asyncOp: {asyncOp} was not present in AsyncOperationToOperationsGroupMap.");
            this.AsyncOperationToOperationsGroupMap[asyncOp].RemoveOperation(asyncOp);
            AsyncOperation oldChainOwner = this.AsyncOperationToOperationsGroupMap[asyncOp].GetOwnerOperation(); // for DEBUGGING
            this.AsyncOperationToOperationsGroupMap[asyncOp.ParentTask].InsertOperation(asyncOp);
            AsyncOperation newChainOwner = this.AsyncOperationToOperationsGroupMap[asyncOp.ParentTask].GetOwnerOperation(); // for DEBUGGING
            this.AsyncOperationToOperationsGroupMap[asyncOp] = this.AsyncOperationToOperationsGroupMap[asyncOp.ParentTask];

            asyncOp.LastMoveNextHandled = true;
            Debug.WriteLine($"===========<IMP_PCTStrategy> [InsertAsyncOperationIntoOperationGroup] CASE_2: moved asyncOp: {asyncOp} from old chain of {oldChainOwner} (priority: {this.PrioritizedOperations.IndexOf(this.AsyncOperationToOperationsGroupMap[oldChainOwner])}) to new chain of {newChainOwner} (priority: {this.PrioritizedOperations.IndexOf(this.AsyncOperationToOperationsGroupMap[newChainOwner])}).");
        }

        /// <summary>
        /// Sets the priority of new operations, if there are any.
        /// </summary>
        private void SetNewOperationPriorities(List<AsyncOperation> ops, AsyncOperation current)
        {
            if (this.AllRegisteredOperations.Count is 0)
            {
                if (current.IsContinuationTask)
                {
                    Debug.WriteLine($"===========<IMP_PCTStrategy> [SetNewOperationPriorities] handling NEW continuation task: {current}.");
                }
                else if (current.IsDelayTaskOperation)
                {
                    Debug.WriteLine($"===========<IMP_PCTStrategy> [SetNewOperationPriorities] handling NEW delay task: {current}.");
                }
                else
                {
                    Debug.WriteLine($"===========<IMP_PCTStrategy> [SetNewOperationPriorities] handling NEW spawn task: {current}.");
                }

                this.InsertNewAsyncOperationIntoOperationGroup(current);
                this.AllRegisteredOperations.Add(current);
            }

            // Randomize the priority of all new operations.
            foreach (var op in ops.Where(op => !this.AllRegisteredOperations.Contains(op)))
            {
                this.AllRegisteredOperations.Add(op);
                if (op.IsContinuationTask)
                {
                    Debug.WriteLine($"===========<IMP_PCTStrategy> [SetNewOperationPriorities] handling NEW continuation task: {op}.");
                }
                else if (op.IsOwnerSpawnOperation)
                {
                    Debug.WriteLine($"===========<IMP_PCTStrategy> [SetNewOperationPriorities] handling NEW spawn task: {op}.");
                }
                else if (current.IsDelayTaskOperation)
                {
                    Debug.WriteLine($"===========<IMP_PCTStrategy> [SetNewOperationPriorities] handling NEW delay task: {op}.");
                }
                else
                {
                    Debug.WriteLine($"===========<IMP_PCTStrategy> [SetNewOperationPriorities] handling NEW non-task operation: {op}.");
                }

                this.InsertNewAsyncOperationIntoOperationGroup(op);
            }

            foreach (var op in this.AllRegisteredOperations.Where(op => !op.LastMoveNextHandled))
            {
                if (op.IsContinuationTask)
                {
                    Debug.WriteLine($"===========<IMP_PCTStrategy> [SetNewOperationPriorities] changing priority due to MoveNext of continuation task: {op}.");
                }
                else if (op.IsOwnerSpawnOperation)
                {
                    Debug.WriteLine($"===========<IMP_PCTStrategy> [SetNewOperationPriorities] changing priority due to MoveNext of spawn task: {op}.");
                }
                else
                {
                    Specification.Assert(false, $"     ===========<IMP_PCTStrategy-ERROR> [InsertAsyncOperationIntoOperationGroup] MoveNext can be called only by either a spaen or continuation task (not delay tasks and non-tasks).");
                }

                this.InsertAsyncOperationIntoOperationGroupOnMoveNext(op);
            }
        }

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
                Debug.WriteLine($"===========<IMP_PCTStrategy> [DeprioritizeEnabledOperationWithHighestPriority] operationGroup of owner op: {this.AsyncOperationToOperationsGroupMap[deprioritizedOperation].GetOwnerOperation()} is deprioritized.");
            }
            else if (isYielding)
            {
                // The current operation is yielding its execution to the next prioritized operation.
                deprioritizedOperation = current;
                // Debug.WriteLine("<PCTLog> operation '{0}' yields its priority.", deprioritizedOperation.Name);
                Debug.WriteLine($"<PCTLog> operationGroup of owner op: {this.AsyncOperationToOperationsGroupMap[deprioritizedOperation].GetOwnerOperation()} yields its priority.");
                Debug.WriteLine($"===========<IMP_PCTStrategy> [DeprioritizeEnabledOperationWithHighestPriority] operationGroup of owner op: {this.AsyncOperationToOperationsGroupMap[deprioritizedOperation].GetOwnerOperation()} yields its priority.");
            }

            if (deprioritizedOperation != null)
            {
                // Deprioritize the operation by putting it in the end of the list.
                this.PrioritizedOperations.Remove(this.AsyncOperationToOperationsGroupMap[deprioritizedOperation]);
                this.PrioritizedOperations.Add(this.AsyncOperationToOperationsGroupMap[deprioritizedOperation]);
                this.ActualNumberOfPrioritySwitches++;
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
                Debug.WriteLine($"===========<IMP_PCTStrategy> [DebugPrintOperationPriorityList].");
                Debug.Write($"                  SIZE of each ASYNC OP GROUP:");
                int maxSizeOperationgroup = 0;
                for (int idx = 0; idx < this.PrioritizedOperations.Count; idx++)
                {
                    if (idx != this.PrioritizedOperations.Count - 1)
                    {
                        Debug.Write($"|Group_ID_{this.PrioritizedOperations[idx].GetTaskGroupID()}|: {this.PrioritizedOperations[idx].GetOperationsChain().Count}, ");
                    }
                    else
                    {
                        Debug.WriteLine($"|Group_ID_{this.PrioritizedOperations[idx].GetTaskGroupID()}|: {this.PrioritizedOperations[idx].GetOperationsChain().Count}.");
                    }

                    maxSizeOperationgroup = Math.Max(maxSizeOperationgroup, this.PrioritizedOperations[idx].GetOperationsChain().Count);
                }

                Debug.WriteLine($"                  MAX_SIZE of any ASYNC OP GROUP (MAX_CHAIN_LENGTH): {maxSizeOperationgroup}");

                Debug.WriteLine($"                  ELEMENTS of each ASYNC OP GROUP:");
                for (int idx = 0; idx < this.PrioritizedOperations.Count; idx++)
                {
                    List<AsyncOperation> operationChain = this.PrioritizedOperations[idx].GetOperationsChain();
                    Debug.Write($"                  Group_ID_{this.PrioritizedOperations[idx].GetTaskGroupID()} (PRIORITY: {idx}): ");
                    for (int jdx = 0; jdx < operationChain.Count; jdx++)
                    {
                        if (jdx != operationChain.Count - 1)
                        {
                            Debug.Write($"{operationChain[jdx].Name} (STATUS: {operationChain[jdx].Status}), ");
                        }
                        else
                        {
                            Debug.WriteLine($"{operationChain[jdx].Name} (STATUS: {operationChain[jdx].Status}).");
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
