using EventStore.Common.Utils;

namespace EventStore.Core.Services.Storage.ReaderIndex {
	public class LogV2StreamIdValidator : IValidator<string> {
		public LogV2StreamIdValidator() {
		}

		public void Validate(string streamId) {
			Ensure.NotNullOrEmpty(streamId, "streamId");
		}
	}
}
