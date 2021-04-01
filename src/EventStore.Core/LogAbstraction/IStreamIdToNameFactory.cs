namespace EventStore.Core.LogAbstraction {
	//qq pretty lame
	public interface IStreamIdToNameFactory<T> {
		IStreamIdToName<T> Create(object input = null);
	}
}
