// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Coyote.Runtime
{
    /// <summary>
    /// The execution scope of an asynchronous operation.
    /// </summary>
    internal enum AsyncOperationScope
    {
        /// <summary>
        /// The operation is executing in the default scope.
        /// </summary>
        Default = 0,

        /// <summary>
        /// The operation is executing in a synchronized scope.
        /// </summary>
        Synchronized
    }
}
