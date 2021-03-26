using System;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Services.UserManagement;

namespace EventStore.Core.Services.PersistentSubscription {
	public class PersistentSubscriptionStreamReader : IPersistentSubscriptionStreamReader {
		public const int MaxPullBatchSize = 500;

		private readonly IODispatcher _ioDispatcher;
		private readonly int _maxPullBatchSize;

		public PersistentSubscriptionStreamReader(IODispatcher ioDispatcher, int maxPullBatchSize) {
			_ioDispatcher = ioDispatcher;
			_maxPullBatchSize = maxPullBatchSize;
		}

		public void BeginReadEvents(IPersistentSubscriptionEventSource eventSource,
			IPersistentSubscriptionStreamPosition startPosition, int countToLoad, int batchSize, bool resolveLinkTos, bool skipFirstEvent,
			Action<ResolvedEvent[], IPersistentSubscriptionStreamPosition, bool> onEventsFound) {
			var actualBatchSize = GetBatchSize(batchSize);

			if (eventSource.FromStream) {
				_ioDispatcher.ReadForward(
					eventSource.EventStreamId, startPosition.StreamEventNumber, Math.Min(countToLoad, actualBatchSize),
					resolveLinkTos, SystemAccounts.System, new ResponseHandler(onEventsFound, skipFirstEvent).FetchCompleted);

			} else if (eventSource.FromAll) {
				_ioDispatcher.ReadAllForward(
					startPosition.TFPosition.Commit,
					startPosition.TFPosition.Prepare,
					Math.Min(countToLoad, actualBatchSize),
					resolveLinkTos,
					true,
					null,
					SystemAccounts.System,
					null,
					new ResponseHandler(onEventsFound, skipFirstEvent).FetchAllCompleted,
					async () => {
						//if the read times out, we just try again
						await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
						BeginReadEvents(eventSource, startPosition, countToLoad, batchSize, resolveLinkTos, skipFirstEvent, onEventsFound);
					},
					Guid.NewGuid());
			} else {
				throw new InvalidOperationException();
			}
		}

		private int GetBatchSize(int batchSize) {
			return Math.Min(Math.Min(batchSize == 0 ? 20 : batchSize, MaxPullBatchSize), _maxPullBatchSize);
		}

		private class ResponseHandler {
			private readonly Action<ResolvedEvent[], IPersistentSubscriptionStreamPosition, bool> _onFetchCompleted;
			private readonly bool _skipFirstEvent;

			public ResponseHandler(Action<ResolvedEvent[], IPersistentSubscriptionStreamPosition, bool> onFetchCompleted, bool skipFirstEvent) {
				_onFetchCompleted = onFetchCompleted;
				_skipFirstEvent = skipFirstEvent;
			}

			public void FetchCompleted(ClientMessage.ReadStreamEventsForwardCompleted msg) {
				_onFetchCompleted(_skipFirstEvent ? msg.Events.Skip(1).ToArray() : msg.Events, new PersistentSubscriptionSingleStreamPosition(msg.NextEventNumber), msg.IsEndOfStream);
			}

			public void FetchAllCompleted(ClientMessage.ReadAllEventsForwardCompleted msg) {
				_onFetchCompleted(_skipFirstEvent ? msg.Events.Skip(1).ToArray() : msg.Events, new PersistentSubscriptionAllStreamPosition(msg.NextPos.CommitPosition, msg.NextPos.PreparePosition), msg.IsEndOfStream);
			}
		}
	}
}
