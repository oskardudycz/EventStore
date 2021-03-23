using System;

namespace EventStore.Core.Services.PersistentSubscription {
	public interface IPersistentSubscriptionEventSource {
		bool FromStream { get; }
		string EventStreamId { get; }
		bool FromAll { get; }
		string ToString();
	}
}
