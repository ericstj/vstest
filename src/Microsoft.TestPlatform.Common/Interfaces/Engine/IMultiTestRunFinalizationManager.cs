﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.Engine
{
    /// <summary>
    /// Orchestrates multi test run finalization operations.
    /// </summary>
    internal interface IMultiTestRunFinalizationManager
    {
        /// <summary>
        /// Finalizes multi test run and provides results through handler
        /// </summary>
        /// <param name="attachments">Collection of attachments</param>
        /// <param name="eventHandler">EventHandler for handling multi test run finalization event</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task FinalizeMultiTestRunAsync(IRequestData requestData, IEnumerable<AttachmentSet> attachments, IMultiTestRunFinalizationEventsHandler eventHandler, CancellationToken cancellationToken);

        /// <summary>
        /// Finalizes multi test run
        /// </summary>
        /// <param name="attachments">Collection of attachments</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of attachments.</returns>
        Task<Collection<AttachmentSet>> FinalizeMultiTestRunAsync(IRequestData requestData, IEnumerable<AttachmentSet> attachments, CancellationToken cancellationToken);
    }
}