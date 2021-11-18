// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Coyote.IO;
using Microsoft.Coyote.Runtime;

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
        private int enabledSpawneesCountMax = 0;

        private int actualNumberOfPrioritySwitches = 0;

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
        /// List of prioritized operations.
        /// </summary>
        private readonly List<AsyncOperation> PrioritizedOperations;

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
            this.PrioritizedOperations = new List<AsyncOperation>();
            this.PriorityChangePoints = new HashSet<int>();
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
                this.ScheduleLength = Math.Max(this.ScheduleLength, this.StepCount);
                this.StepCount = 0;

                this.PrioritizedOperations.Clear();
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

        /// <inheritdoc/>
        internal override bool GetNextOperation(IEnumerable<AsyncOperation> ops, AsyncOperation current,
            bool isYielding, out AsyncOperation next)
        {
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
            string envPrintEnabledChildrenDepth = Environment.GetEnvironmentVariable("MYCOYOTE_PRINT_MAX_ENABLED_CHILDS"); // NOTE: OLP_TEST_PCT_SWITCHES muse be a positive integer.
            bool envPrintEnabledChildrenDepthBool = false;
            if (envPrintEnabledChildrenDepth != null)
            {
                envPrintEnabledChildrenDepthBool = bool.Parse(envPrintEnabledChildrenDepth);
            }

            if (envPrintEnabledChildrenDepthBool)
            {
                this.DebugPrintMaxEnabledSpawneeDepth(enabledOps);
            }

            return true;
        }

        /// <summary>
        /// Sets the priority of new operations, if there are any.
        /// </summary>
        private void SetNewOperationPriorities(List<AsyncOperation> ops, AsyncOperation current)
        {
            if (this.PrioritizedOperations.Count is 0)
            {
                this.PrioritizedOperations.Add(current);
            }

            // Randomize the priority of all new operations.
            foreach (var op in ops.Where(op => !this.PrioritizedOperations.Contains(op)))
            {
                int index = 0;
                string envNewPCT = Environment.GetEnvironmentVariable("MYCOYOTE_NEW_PCT"); // NOTE: MYCOYOTE_NEW_PCT must be a either 0 or 1.
                bool envNewPCTBool = false;
                if (envNewPCT != null)
                {
                    envNewPCTBool = bool.Parse(envNewPCT);
                }

                if (!envNewPCTBool)
                {
                    index = this.RandomValueGenerator.Next(this.PrioritizedOperations.Count) + 1;
                }
                else
                {
                    // TODO: refactor, remove above line somehow
                    // TODO: think of cases with mix of tasks and actors, where op would be TaskOperation only sometimes
                    if (op.Spawner == null)
                    {
                        // Randomly choose a priority for this operation.
                        // TODO: Experiment with +1, -1, +rand(1,-1), random between children
                        index = this.RandomValueGenerator.Next(this.PrioritizedOperations.Count) + 1;
                    }
                    else
                    {
                        int beginIndex = 0;
                        int endIndex = this.PrioritizedOperations.Count;
                        foreach (var opp in this.PrioritizedOperations)
                        {
                            if (opp.Spawner == op.Spawner || opp == op.Spawner)
                            {
                                beginIndex = this.PrioritizedOperations.IndexOf(opp);
                                break;
                            }
                        }

                        foreach (var opp in this.PrioritizedOperations)
                        {
                            if (opp.Spawner == op.Spawner || opp == op.Spawner)
                            {
                                endIndex = this.PrioritizedOperations.IndexOf(opp);
                            }
                        }

                        if (beginIndex == endIndex)
                        {
                            index = beginIndex + this.RandomValueGenerator.Next(1);
                        }
                        else
                        {
                            index = beginIndex + this.RandomValueGenerator.Next(endIndex + 2);
                        }

                        if (index < 0)
                        {
                            index = 0;
                        }

                        if (index > this.PrioritizedOperations.Count)
                        {
                            index = this.PrioritizedOperations.Count;
                        }

                        // index = this.PrioritizedOperations.IndexOf(op.Spawner) + 1;
                    }
                }

                this.PrioritizedOperations.Insert(index, op);
                Console.WriteLine("<PCTLog> chose priority '{0}' for new operation '{1}'.", index, op.Name);
            }
        }

        private void DebugPrintMaxEnabledSpawneeDepth(List<AsyncOperation> enabledOps)
        {
            foreach (var op in this.PrioritizedOperations)
            {
                int enabledSpawneesCount = 0;
                foreach (var spawnee in op.Spawnees.Where(spawnee => enabledOps.Contains(spawnee)))
                {
                    enabledSpawneesCount++;
                }

                if (this.enabledSpawneesCountMax < enabledSpawneesCount)
                {
                    this.enabledSpawneesCountMax = enabledSpawneesCount;
                    string folder = @"C:\Users\t-fnayyar\Desktop\repos\olp-buggy-branches\";
                    string fileName = "maxEnabledChildDepth.txt";
                    string fullPath = folder + fileName;
                    File.WriteAllText(fullPath, $"{this.enabledSpawneesCountMax} \n");
                }
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
                Debug.WriteLine("<PCTLog> operation '{0}' is deprioritized.", deprioritizedOperation.Name);
            }
            else if (isYielding)
            {
                // The current operation is yielding its execution to the next prioritized operation.
                deprioritizedOperation = current;
                Debug.WriteLine("<PCTLog> operation '{0}' yields its priority.", deprioritizedOperation.Name);
            }

            if (deprioritizedOperation != null)
            {
                string envNewPCT = Environment.GetEnvironmentVariable("MYCOYOTE_NEW_PCT"); // NOTE: MYCOYOTE_NEW_PCT must be a either 0 or 1.
                bool envNewPCTBool = false;
                if (envNewPCT != null)
                {
                    envNewPCTBool = bool.Parse(envNewPCT);
                }

                if (!envNewPCTBool)
                {
                    // Deprioritize the operation by putting it in the end of the list.
                    this.PrioritizedOperations.Remove(deprioritizedOperation);
                    this.PrioritizedOperations.Add(deprioritizedOperation);
                }
                else
                {
                    string envPrintNumOfPrioritySwtichPoints = Environment.GetEnvironmentVariable("MYCOYOTE_PRINT_PRIORITY_SWITCHES"); // NOTE: OLP_TEST_PCT_SWITCHES muse be a positive integer.
                    bool envPrintNumOfPrioritySwtichPointsBool = false;
                    if (envPrintNumOfPrioritySwtichPoints != null)
                    {
                        envPrintNumOfPrioritySwtichPointsBool = bool.Parse(envPrintNumOfPrioritySwtichPoints);
                    }

                    if (envPrintNumOfPrioritySwtichPointsBool)
                    {
                        this.actualNumberOfPrioritySwitches++;
                        string folder = @"C:\Users\t-fnayyar\Desktop\repos\olp-buggy-branches\";
                        string fileName = "actualNumberOfPrioritySwitches.txt";
                        string fullPath = folder + fileName;
                        File.WriteAllText(fullPath, $"{this.actualNumberOfPrioritySwitches} \n");
                    }

                    // Deprioritize the operation by putting it in the end of the list.

                    /*this.PrioritizedOperations.Remove(deprioritizedOperation);
                    this.PrioritizedOperations.Add(deprioritizedOperation);
                    foreach (AsyncOperation spawnee in deprioritizedOperation.Spawnees)
                    {
                        this.PrioritizedOperations.Remove(spawnee);
                        this.PrioritizedOperations.Add(spawnee);
                    }*/

                    LinkedList<AsyncOperation> toDeprioratize = new LinkedList<AsyncOperation>();
                    foreach (AsyncOperation aop in this.PrioritizedOperations)
                    {
                        if (aop == deprioritizedOperation || deprioritizedOperation.Spawnees.Contains(aop))
                        {
                            toDeprioratize.AddLast(aop);
                        }
                    }

                    foreach (AsyncOperation aop in toDeprioratize)
                    {
                        this.PrioritizedOperations.Remove(aop);
                        this.PrioritizedOperations.Add(aop);
                    }
                }
            }
        }

        /// <summary>
        /// Returns the enabled operation with the highest priority.
        /// </summary>
        private AsyncOperation GetEnabledOperationWithHighestPriority(List<AsyncOperation> ops)
        {
            foreach (var entity in this.PrioritizedOperations)
            {
                if (ops.Any(m => m == entity))
                {
                    return entity;
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
                    if (idx < this.PrioritizedOperations.Count - 1)
                    {
                        Debug.Write("'{0}', ", this.PrioritizedOperations[idx].Name);
                    }
                    else
                    {
                        Debug.WriteLine("'{0}'.", this.PrioritizedOperations[idx].Name);
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
