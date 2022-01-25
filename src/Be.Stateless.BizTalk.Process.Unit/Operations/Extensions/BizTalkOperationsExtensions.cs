﻿#region Copyright & License

// Copyright © 2012 - 2020 François Chabot
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#endregion

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Be.Stateless.Linq.Extensions;
using log4net;
using Microsoft.BizTalk.Operations;

namespace Be.Stateless.BizTalk.Operations.Extensions
{
	public static class BizTalkOperationsExtensions
	{
		public static MessageBoxServiceInstance[] GetRunningOrSuspendedServiceInstances()
		{
			using (var bizTalkOperations = new BizTalkOperations())
			{
				return bizTalkOperations
					.GetServiceInstances().OfType<MessageBoxServiceInstance>()
					.Where(i => (i.InstanceStatus & (InstanceStatus.RunningAll | InstanceStatus.SuspendedAll)) != InstanceStatus.None)
					.ToArray();
			}
		}

		[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
		public static void TerminateUncompletedBizTalkServiceInstances()
		{
			// https://blogs.msdn.microsoft.com/biztalknotes/2015/05/19/biztalk-2013-r2-issues-while-terminating-and-resuming-instance-using-c-code/
			// http://stackoverflow.com/questions/27454762/biztalk-operations-terminateinstance-sql-error
			// From BizTalk 2010 onwards, it was decided to acquire a lock only on Update statement and not on Select
			// statements. Therefore the workaround is to create 2 BizTalkOperations objects. One for the Select operation
			// methods and other for Update Operations methods.
			using (var bizTalkOperations = new BizTalkOperations())
			{
				GetRunningOrSuspendedServiceInstances()
					.Select(i => new { ServiceInstance = i, CompletionStatus = bizTalkOperations.TerminateInstance(i.ID) })
					.Where(sd => sd.CompletionStatus != CompletionStatus.Succeeded)
					.ForEach(
						(idx, sd) => {
							Trace.TraceWarning("Could not terminate the BizTalk service instance with ID {0}", sd.ServiceInstance.ID);
							_logger.Warn(
								$"[{idx,2}] Could not terminate the BizTalk service instance class: {sd.ServiceInstance.Class}\r\n"
								+ $"     ServiceType: {sd.ServiceInstance.ServiceType}\r\n"
								+ $"     Creation Time: {sd.ServiceInstance.CreationTime}\r\n"
								+ $"     Status: {sd.ServiceInstance.InstanceStatus}\r\n"
								+ $"     Error: {sd.ServiceInstance.ErrorDescription}\r\n");
						}
					);
			}
		}

		private static readonly ILog _logger = LogManager.GetLogger(typeof(BizTalkOperationsExtensions));
	}
}
