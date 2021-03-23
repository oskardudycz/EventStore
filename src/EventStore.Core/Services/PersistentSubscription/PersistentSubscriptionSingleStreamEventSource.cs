using System;

namespace EventStore.Core.Services.PersistentSubscription {
	public class PersistentSubscriptionSingleStreamEventSource : IPersistentSubscriptionEventSource {
		public bool FromStream => true;
		public string EventStreamId { get; }
		public bool FromAll => false;

		public PersistentSubscriptionSingleStreamEventSource(string eventStreamId) {
			EventStreamId = eventStreamId ?? throw new ArgumentNullException(nameof(eventStreamId));
		}
		public override string ToString() => EventStreamId;
	}
}
