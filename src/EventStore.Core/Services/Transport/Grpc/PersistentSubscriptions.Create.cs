using System;
using System.Threading.Tasks;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Client;
using EventStore.Client.PersistentSubscriptions;
using EventStore.Plugins.Authorization;
using Grpc.Core;
using static EventStore.Core.Messages.ClientMessage.CreatePersistentSubscriptionToStreamCompleted;

namespace EventStore.Core.Services.Transport.Grpc {
	partial class PersistentSubscriptions {
		private static readonly Operation CreateOperation = new Operation(Plugins.Authorization.Operations.Subscriptions.Create);

		public override async Task<CreateResp> Create(CreateReq request, ServerCallContext context) {
			var createPersistentSubscriptionSource = new TaskCompletionSource<CreateResp>();
			var settings = request.Options.Settings;
			var correlationId = Guid.NewGuid();

			var user = context.GetHttpContext().User;
			
			if (!await _authorizationProvider.CheckAccessAsync(user,
				CreateOperation, context.CancellationToken).ConfigureAwait(false)) {
				throw AccessDenied();
			}
			_publisher.Publish(new ClientMessage.CreatePersistentSubscriptionToStream(
				correlationId,
				correlationId,
				new CallbackEnvelope(HandleCreatePersistentSubscriptionCompleted),
				request.Options.StreamIdentifier,
				request.Options.GroupName,
				settings.ResolveLinks,
				new StreamRevision(settings.Revision).ToInt64(),
				settings.MessageTimeoutCase switch {
					CreateReq.Types.Settings.MessageTimeoutOneofCase.MessageTimeoutMs => settings.MessageTimeoutMs,
					CreateReq.Types.Settings.MessageTimeoutOneofCase.MessageTimeoutTicks => (int)TimeSpan
						.FromTicks(settings.MessageTimeoutTicks).TotalMilliseconds,
					_ => 0
				},
				settings.ExtraStatistics,
				settings.MaxRetryCount,
				settings.HistoryBufferSize,
				settings.LiveBufferSize,
				settings.ReadBatchSize,
				settings.CheckpointAfterCase switch {
					CreateReq.Types.Settings.CheckpointAfterOneofCase.CheckpointAfterMs => settings.CheckpointAfterMs,
					CreateReq.Types.Settings.CheckpointAfterOneofCase.CheckpointAfterTicks => (int)TimeSpan
						.FromTicks(settings.CheckpointAfterTicks).TotalMilliseconds,
					_ => 0
				},
				settings.MinCheckpointCount,
				settings.MaxCheckpointCount,
				settings.MaxSubscriberCount,
				settings.NamedConsumerStrategy.ToString(),
				user));

			return await createPersistentSubscriptionSource.Task.ConfigureAwait(false);

			void HandleCreatePersistentSubscriptionCompleted(Message message) {
				if (message is ClientMessage.NotHandled notHandled && RpcExceptions.TryHandleNotHandled(notHandled, out var ex)) {
					createPersistentSubscriptionSource.TrySetException(ex);
					return;
				}

				if (!(message is ClientMessage.CreatePersistentSubscriptionToStreamCompleted completed)) {
					createPersistentSubscriptionSource.TrySetException(
						RpcExceptions.UnknownMessage<ClientMessage.CreatePersistentSubscriptionToStreamCompleted>(message));
					return;
				}

				switch (completed.Result) {
					case CreatePersistentSubscriptionToStreamResult.Success:
						createPersistentSubscriptionSource.TrySetResult(new CreateResp());
						return;
					case CreatePersistentSubscriptionToStreamResult.Fail:
						createPersistentSubscriptionSource.TrySetException(RpcExceptions.PersistentSubscriptionFailed(request.Options.StreamIdentifier, request.Options.GroupName, completed.Reason));
						return;
					case CreatePersistentSubscriptionToStreamResult.AlreadyExists:
						createPersistentSubscriptionSource.TrySetException(RpcExceptions.PersistentSubscriptionExists(request.Options.StreamIdentifier, request.Options.GroupName));
						return;
					case CreatePersistentSubscriptionToStreamResult.AccessDenied:
						createPersistentSubscriptionSource.TrySetException(RpcExceptions.AccessDenied());
						return;
					default:
						createPersistentSubscriptionSource.TrySetException(RpcExceptions.UnknownError(completed.Result));
						return;
				}
			}
		}
	}
}
