// Copyright 2007-2008 The Apache Software Foundation.
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use 
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed 
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace HeavyLoad.Load
{
	using System;
	using System.Threading;
	using Castle.Windsor;
	using MassTransit;
	using MassTransit.Transports.Msmq;

    public class LocalLoadTest : IDisposable
	{
		private const int _repeatCount = 5000;
		private readonly ManualResetEvent _completeEvent = new ManualResetEvent(false);
		private readonly IWindsorContainer _container;
		private readonly ManualResetEvent _responseEvent = new ManualResetEvent(false);

		private readonly IServiceBus _bus;
		private int _requestCounter = 0;
		private int _responseCounter = 0;

		public LocalLoadTest()
		{
			_container = new HeavyLoadContainer();

			_bus = _container.Resolve<IServiceBus>();

			MsmqEndpoint endpoint = _bus.Endpoint as MsmqEndpoint;
			if (endpoint != null)
                MsmqUtilities.ValidateAndPurgeQueue(endpoint.QueuePath);
		}

		public void Dispose()
		{
			_bus.Dispose();
			_container.Dispose();
		}

		public void Run(StopWatch stopWatch)
		{
			_bus.Subscribe<GeneralMessage>(Handle);
			_bus.Subscribe<SimpleResponse>(Handler);

			stopWatch.Start();

			CheckPoint publishCheckpoint = stopWatch.Mark("Sending " + _repeatCount + " messages");
			CheckPoint receiveCheckpoint = stopWatch.Mark("Request/Response " + _repeatCount + " messages");

			for (int index = 0; index < _repeatCount; index++)
			{
				_bus.Publish(new GeneralMessage());
			}

			publishCheckpoint.Complete(_repeatCount);

			bool completed = _completeEvent.WaitOne(TimeSpan.FromSeconds(60), true);

			bool responseCompleted = _responseEvent.WaitOne(TimeSpan.FromSeconds(60), true);

			receiveCheckpoint.Complete(_requestCounter + _responseCounter);

			stopWatch.Stop();
		}

		private void Handler(SimpleResponse obj)
		{
			Interlocked.Increment(ref _responseCounter);
			if (_responseCounter == _repeatCount)
				_responseEvent.Set();
		}

		private void Handle(GeneralMessage obj)
		{
			_bus.Publish(new SimpleResponse());

			Interlocked.Increment(ref _requestCounter);
			if (_requestCounter == _repeatCount)
				_completeEvent.Set();
		}
	}
}